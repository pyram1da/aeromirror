/* Positive lifecycle checks against the production HTTP owner. Test sockets
 * are loopback-only; no AirPlay payload, pairing or phone content is used. */
#define netutils_init_socket production_netutils_init_socket
#include "netutils.c"
#undef netutils_init_socket

/* Keep all other production network helpers. Only constrain test binding. */
int netutils_init_socket(unsigned short *port, int ipv6, int udp) {
    if (ipv6 || udp) return -1;
    SOCKET fd = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
    if (fd == INVALID_SOCKET) return -1;
    struct sockaddr_in address = {0};
    address.sin_family = AF_INET;
    address.sin_addr.s_addr = htonl(INADDR_LOOPBACK);
    address.sin_port = htons(*port);
    if (bind(fd, (const struct sockaddr *)&address, sizeof(address))) {
        closesocket(fd);
        return -1;
    }
    int length = sizeof(address);
    if (getsockname(fd, (struct sockaddr *)&address, &length)) {
        closesocket(fd);
        return -1;
    }
    *port = ntohs(address.sin_port);
    return (int)fd;
}

#include "httpd.c"

#define CHECK(test) do { if (!(test)) { \
    fprintf(stderr, "FAIL line %d: %s\n", __LINE__, #test); exit(1); \
} } while (0)

typedef struct test_state_s {
    httpd_t *server;
    HANDLE entered, release, completed, destroy_entered, destroy_release;
    volatile LONG hold_request, hold_destroy, created, destroyed;
    uint64_t result_session, result_request;
    int result;
} test_state_t;

typedef struct test_connection_s {
    test_state_t *state;
    uint64_t session;
} test_connection_t;

static void wait_event(HANDLE event) {
    CHECK(WaitForSingleObject(event, 5000) == WAIT_OBJECT_0);
}

static void *test_init(void *opaque, unsigned char *local, int local_length,
                        unsigned char *remote, int remote_length, unsigned int zone) {
    (void)local; (void)local_length; (void)remote; (void)remote_length; (void)zone;
    test_connection_t *connection = calloc(1, sizeof(*connection));
    CHECK(connection);
    connection->state = opaque;
    InterlockedIncrement(&connection->state->created);
    return connection;
}

static void test_request(void *opaque, http_request_t *request,
                         http_response_t **response) {
    test_connection_t *connection = opaque;
    test_state_t *state = connection->state;
    if (InterlockedExchange(&state->hold_request, 0)) {
        SetEvent(state->entered);
        wait_event(state->release);
    }
    if (!strcmp(http_request_get_url(request), "/bind")) {
        connection->session = httpd_allocate_session_id(state->server);
        CHECK(connection->session);
        CHECK(httpd_bind_session(state->server, connection, connection->session));
        CHECK(httpd_set_connection_type(state->server, connection,
                                         CONNECTION_TYPE_RAOP) >= 0);
    }
    char session[32];
    snprintf(session, sizeof(session), "%llu",
             (unsigned long long)connection->session);
    *response = http_response_create();
    CHECK(*response);
    http_response_init(*response, "RTSP/1.0", 200, "OK");
    http_response_add_header(*response, "CSeq", "1");
    http_response_add_header(*response, "X-Test-Session", session);
    http_response_finish(*response, NULL, 0);
}

static void test_destroy(void *opaque) {
    test_connection_t *connection = opaque;
    test_state_t *state = connection->state;
    if (InterlockedExchange(&state->hold_destroy, 0)) {
        SetEvent(state->destroy_entered);
        wait_event(state->destroy_release);
    }
    InterlockedIncrement(&state->destroyed);
    free(connection);
}

static void test_result(void *opaque, uint64_t session, uint64_t request, int result) {
    test_state_t *state = opaque;
    state->result_session = session;
    state->result_request = request;
    state->result = result;
    SetEvent(state->completed);
}

static SOCKET connect_client(unsigned short port) {
    SOCKET fd = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
    CHECK(fd != INVALID_SOCKET);
    DWORD timeout = 5000;
    CHECK(!setsockopt(fd, SOL_SOCKET, SO_RCVTIMEO, (const char *)&timeout, sizeof(timeout)));
    CHECK(!setsockopt(fd, SOL_SOCKET, SO_SNDTIMEO, (const char *)&timeout, sizeof(timeout)));
    struct sockaddr_in address = {0};
    address.sin_family = AF_INET;
    address.sin_addr.s_addr = htonl(INADDR_LOOPBACK);
    address.sin_port = htons(port);
    CHECK(!connect(fd, (const struct sockaddr *)&address, sizeof(address)));
    return fd;
}

static void send_request(SOCKET fd, int bind_session) {
    const char *request = bind_session
        ? "OPTIONS /bind RTSP/1.0\r\nCSeq: 1\r\nContent-Length: 0\r\n\r\n"
        : "OPTIONS /ping RTSP/1.0\r\nCSeq: 1\r\nContent-Length: 0\r\n\r\n";
    int length = (int)strlen(request);
    CHECK(send(fd, request, length, 0) == length);
}

static uint64_t read_response(SOCKET fd) {
    char response[1024] = {0};
    int length = 0;
    while (!strstr(response, "\r\n\r\n")) {
        CHECK(length < (int)sizeof(response) - 1);
        int count = recv(fd, response + length, sizeof(response) - length - 1, 0);
        CHECK(count > 0);
        length += count;
    }
    CHECK(strstr(response, "RTSP/1.0 200 OK"));
    const char *session = strstr(response, "X-Test-Session: ");
    CHECK(session);
    return strtoull(session + strlen("X-Test-Session: "), NULL, 10);
}

static uint64_t roundtrip(SOCKET fd, int bind_session) {
    send_request(fd, bind_session);
    return read_response(fd);
}

static void expect_result(test_state_t *state, uint64_t session,
                           uint64_t request, int result) {
    wait_event(state->completed);
    CHECK(state->result_session == session);
    CHECK(state->result_request == request);
    CHECK(state->result == result);
}

static DWORD WINAPI stop_listener(LPVOID opaque) {
    httpd_stop((httpd_t *)opaque);
    return 0;
}

int main(void) {
    CHECK(!netutils_init());
    logger_t *logger = logger_init();
    CHECK(logger);
    logger_set_level(logger, LOGGER_ERR);
    test_state_t state = {0};
    state.entered = CreateEvent(NULL, FALSE, FALSE, NULL);
    state.release = CreateEvent(NULL, FALSE, FALSE, NULL);
    state.completed = CreateEvent(NULL, FALSE, FALSE, NULL);
    state.destroy_entered = CreateEvent(NULL, FALSE, FALSE, NULL);
    state.destroy_release = CreateEvent(NULL, FALSE, FALSE, NULL);
    CHECK(state.entered && state.release && state.completed &&
          state.destroy_entered && state.destroy_release);
    httpd_callbacks_t callbacks = {0};
    callbacks.opaque = &state;
    callbacks.conn_init = test_init;
    callbacks.conn_request = test_request;
    callbacks.conn_destroy = test_destroy;
    callbacks.session_close_result = test_result;
    state.server = httpd_init(logger, &callbacks, 0);
    CHECK(state.server);
    CHECK(!httpd_request_session_close(state.server, 1, 1));
    unsigned short port = 0;
    CHECK(httpd_start(state.server, &port) > 0 && port);
    int listener = state.server->server_fd4;
    SOCKET first = connect_client(port), second = connect_client(port);
    uint64_t a = roundtrip(first, 1), b = roundtrip(second, 1);
    CHECK(a && b > a);
    CHECK(httpd_request_session_close(state.server, a, 101));
    expect_result(&state, a, 101, 1);
    CHECK(state.destroyed == 1);
    char byte;
    CHECK(recv(first, &byte, 1, 0) == 0);
    closesocket(first);
    CHECK(roundtrip(second, 0) == b);
    CHECK(state.server->server_fd4 == listener);
    CHECK(httpd_request_session_close(state.server, a, 102));
    expect_result(&state, a, 102, 0);
    CHECK(roundtrip(second, 0) == b);
    puts("PASS exact close, peer retained, stale completion, same listener");

    InterlockedExchange(&state.hold_request, 1);
    send_request(second, 1);
    wait_event(state.entered);
    CHECK(httpd_request_session_close(state.server, b, 103));
    CHECK(!httpd_request_session_close(state.server, b, 999));
    SetEvent(state.release);
    uint64_t replacement = read_response(second);
    CHECK(replacement > b);
    expect_result(&state, b, 103, 0);
    CHECK(roundtrip(second, 0) == replacement);
    CHECK(state.destroyed == 1);
    puts("PASS same-socket replacement survives old close; mailbox bounded");

    InterlockedExchange(&state.hold_destroy, 1);
    CHECK(httpd_request_session_close(state.server, replacement, 104));
    wait_event(state.destroy_entered);
    CHECK(WaitForSingleObject(state.completed, 0) == WAIT_TIMEOUT);
    SOCKET next = connect_client(port);
    send_request(next, 1);
    CHECK(state.created == 2); /* accept waits until the old drain completes */
    SetEvent(state.destroy_release);
    expect_result(&state, replacement, 104, 1);
    uint64_t previous = read_response(next);
    CHECK(previous > replacement);
    CHECK(state.destroyed == 2);
    closesocket(second);
    puts("PASS result follows destruction; next accept follows drain");

    for (uint64_t iteration = 0; iteration < 50; ++iteration) {
        CHECK(httpd_request_session_close(state.server, previous, 200 + iteration));
        expect_result(&state, previous, 200 + iteration, 1);
        CHECK(recv(next, &byte, 1, 0) == 0);
        closesocket(next);
        CHECK(state.server->server_fd4 == listener);
        next = connect_client(port);
        uint64_t current = roundtrip(next, 1);
        CHECK(current > previous);
        previous = current;
    }
    puts("PASS 50 close/reconnect cycles without listener or port replacement");

    InterlockedExchange(&state.hold_request, 1);
    send_request(next, 0);
    wait_event(state.entered);
    CHECK(httpd_request_session_close(state.server, previous, 300));
    HANDLE stopper = CreateThread(NULL, 0, stop_listener, state.server, 0, NULL);
    CHECK(stopper);
    ULONGLONG deadline = GetTickCount64() + 5000;
    while (worker_lifecycle_should_run(&state.server->lifecycle)) {
        CHECK(GetTickCount64() < deadline);
        Sleep(1);
    }
    SetEvent(state.release);
    expect_result(&state, previous, 300, -1);
    wait_event(stopper);
    CloseHandle(stopper);
    closesocket(next);
    CHECK(!httpd_request_session_close(state.server, previous, 301));
    port = 0;
    CHECK(httpd_start(state.server, &port) > 0);
    next = connect_client(port);
    CHECK(roundtrip(next, 1) > previous);
    httpd_stop(state.server);
    closesocket(next);
    CHECK(state.created == state.destroyed);
    puts("PASS stop cancellation and no ID reuse after explicit listener restart");
    httpd_destroy(state.server);
    CloseHandle(state.entered); CloseHandle(state.release);
    CloseHandle(state.completed); CloseHandle(state.destroy_entered);
    CloseHandle(state.destroy_release);
    logger_destroy(logger);
    netutils_cleanup();
    puts("Native session-close lifecycle checks passed.");
    return 0;
}
