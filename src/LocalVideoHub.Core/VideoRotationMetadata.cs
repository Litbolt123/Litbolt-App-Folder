using System.IO;
using MetadataExtractor;
using MetadataExtractor.Formats.QuickTime;

namespace LocalVideoHub;

internal static class VideoRotationMetadata
{
    /// <summary>Display correction in degrees (0, 90, 180, 270) from container metadata.</summary>
    public static int TryGetCorrectionDegrees(string path)
    {
        if (!File.Exists(path))
            return 0;

        try
        {
            foreach (var directory in ImageMetadataReader.ReadMetadata(path))
            {
                if (directory is QuickTimeTrackHeaderDirectory qt)
                {
                    if (qt.TryGetInt32(QuickTimeTrackHeaderDirectory.TagRotation, out var rotation))
                        return NormalizeCorrection(rotation);
                }

                foreach (var tag in directory.Tags)
                {
                    if (!string.Equals(tag.Name, "Rotation", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (int.TryParse(tag.Description, out var deg))
                        return NormalizeCorrection(deg);
                }
            }
        }
        catch
        {
            /* ignore unreadable metadata */
        }

        return 0;
    }

    private static int NormalizeCorrection(int metadataDegrees)
    {
        var deg = ((metadataDegrees % 360) + 360) % 360;
        return deg switch
        {
            90 or 270 or 180 => deg,
            _ => 0,
        };
    }
}
