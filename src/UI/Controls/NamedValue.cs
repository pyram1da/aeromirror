namespace AirPlayReceiverMvp
{
    internal sealed class NamedValue
    {
        public readonly string Name;
        public readonly string Value;
        public readonly string Subtitle;
        public NamedValue(string name, string value)
            : this(name, value, "") { }
        public NamedValue(string name, string value, string subtitle)
        {
            Name = name;
            Value = value;
            Subtitle = subtitle ?? "";
        }
        public override string ToString() { return Name; }
    }
}
