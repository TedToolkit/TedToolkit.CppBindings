using TedToolkit.CppBindings.Smlib;

using var box = Solids.CreateBox(1, 2, 3, 2, 3, 4);
var volume = box.ComputeVolume(1e-7);
if (!double.IsFinite(volume) || Math.Abs(volume - 24) > 1e-6)
{
    throw new InvalidOperationException($"Expected a volume of 24, found {volume}.");
}

Console.WriteLine($"SMLib box volume: {volume}");
