using System;
using System.Drawing;

namespace AirPlayReceiverMvp
{
    /// <summary>
    /// Pure geometry rules for the foreign renderer window. These values are
    /// product invariants, not user preferences, so they deliberately do not
    /// live in settings.ini.
    /// </summary>
    internal static class RendererPresentationPolicy
    {
        internal const double ModernIPhonePortraitAspect = 9.0 / 19.5;
        internal const double DeviceFrameAspectTolerance = 0.03;
        internal const int NormalScalePermille = 1000;

        internal static readonly Size ProvisionalPortraitSize =
            new Size(900, 1950);

        internal static bool IsKnownPhotosCanvas(
            int width0, int height0,
            int sourceWidth, int sourceHeight,
            int auxiliaryWidth, int auxiliaryHeight,
            int encodedWidth, int encodedHeight)
        {
            // This is an observed transport signature, not a content rectangle.
            // It may control outer-window orientation but must never authorize
            // cropping pixels from the mirrored frame.
            return width0 == 3840 && height0 == 2160 &&
                sourceWidth == 3840 && sourceHeight == 2160 &&
                auxiliaryWidth == 0 && auxiliaryHeight == 0 &&
                encodedWidth == 3840 && encodedHeight == 2160;
        }

        internal static bool HaveEquivalentDeviceAspect(
            Size first, Size second)
        {
            double firstAspect = NormalizedAspect(first);
            double secondAspect = NormalizedAspect(second);
            return firstAspect > 0.0 && secondAspect > 0.0 &&
                Math.Abs(firstAspect - secondAspect) <=
                    DeviceFrameAspectTolerance;
        }

        internal static bool IsLikelyModernIPhoneFrame(Size videoSize)
        {
            double aspect = NormalizedAspect(videoSize);
            return aspect > 0.0 &&
                Math.Abs(aspect - ModernIPhonePortraitAspect) <=
                    DeviceFrameAspectTolerance;
        }

        internal static double NormalizedAspect(Size videoSize)
        {
            if (videoSize.Width <= 0 || videoSize.Height <= 0)
                return 0.0;
            return (double)Math.Min(videoSize.Width, videoSize.Height) /
                Math.Max(videoSize.Width, videoSize.Height);
        }
    }
}
