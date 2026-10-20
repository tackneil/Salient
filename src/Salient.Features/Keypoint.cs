namespace Salient.Features;

/// <summary>
/// A detected feature point. Conventions (ADR-0003):
/// <list type="bullet">
/// <item><see cref="X"/>, <see cref="Y"/> are in input-image pixels, with (0, 0) at the center of the top-left pixel and Y pointing down.</item>
/// <item><see cref="Scale"/> is the σ of the detection scale, in input-image pixels.</item>
/// <item><see cref="Angle"/> is the orientation in degrees, counterclockwise as seen on screen, in [0, 360). It is 0 when the algorithm assigns no orientation.</item>
/// <item><see cref="Response"/> is the detector's strength measure; for SIFT, the contrast |D(x̂)| with intensities in [0, 1].</item>
/// </list>
/// </summary>
/// <param name="X">Horizontal position in input-image pixels.</param>
/// <param name="Y">Vertical position in input-image pixels.</param>
/// <param name="Scale">Detection σ in input-image pixels.</param>
/// <param name="Angle">Orientation in degrees, counterclockwise on screen.</param>
/// <param name="Response">Detector response.</param>
/// <param name="Octave">Pyramid octave; −1 for an upsampled first octave.</param>
/// <param name="Layer">Layer within the octave.</param>
public readonly record struct Keypoint(float X, float Y, float Scale, float Angle, float Response, int Octave, int Layer);
