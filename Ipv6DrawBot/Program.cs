using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

const int IMG_SIZE = 256;
const int SECTOR_COUNT = 8;

int pixelsPerSector = IMG_SIZE / SECTOR_COUNT;

Rgba32 transparentColor = Color.Transparent;

var sectors = new List<Sector>();

string file = string.Join(" ", args);

if(!File.Exists(file))
{
    Console.WriteLine($"file \"{file}\" does not exist");
    Console.ReadKey();
}

using Image<Rgba32> originalImage = Image.Load<Rgba32>(file);
originalImage.Mutate(x => x.Resize(new Size(IMG_SIZE, IMG_SIZE)));

originalImage.ProcessPixelRows(accessor =>
{
    for (int y = 0; y < accessor.Height; y++)
    {
        Span<Rgba32> pixelRow = accessor.GetRowSpan(y);
        for (int x = 0; x < pixelRow.Length; x++)
        {
            int currX = x * 2;
            int currY = y * 2;

            ref Rgba32 colorData = ref pixelRow[x];

            if (colorData == transparentColor) continue;

            int sectorX = currX / pixelsPerSector;
            int sectorY = currY / pixelsPerSector;

            var pixel = new Pixel
            {
                R = colorData.R,
                B = colorData.B,
                G = colorData.G,
                X = currX,
                Y = currY,
                //lazy transparency effect
                PreComputedAddress = IPAddress.Parse($"2602:fa9b:202:{(colorData.A > 240 ? "2" : "1")}{currX.ToString("x3")}:{currY.ToString("x3")}:{colorData.R.ToString("x2")}:{colorData.G.ToString("x2")}:{colorData.B.ToString("x2")}")
            };

            var sector = sectors.Find(x => x.X == sectorX && x.Y == sectorY);
            if (sector == null)
            {
                sector = new Sector { X = sectorX, Y = sectorY };
                sectors.Add(sector);
            }

            sector.Pixels.Add(pixel);
        }
    }
});

foreach (var sector in sectors)
{
    sector.VariationSum = ComputePixelVariance(sector.Pixels);
    sector.Pixels = sector.Pixels.OrderBy(x => Guid.NewGuid().GetHashCode()).ToList();
}

sectors = sectors.OrderBy(x => x.VariationSum).ToList();

bool rawAllowed = false;

try
{
    new Socket(AddressFamily.InterNetworkV6, SocketType.Raw, ProtocolType.IcmpV6).Dispose();
    rawAllowed = true;
}
catch { }

Console.WriteLine($"processors: {Environment.ProcessorCount}, raw allowed: {rawAllowed}");

while (true)
{
    await Parallel.ForEachAsync(sectors, new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount }, async (sector, _) =>
    {
        try
        {
            var promises = new List<Task>();
            for (int i = 0; i < sector.Pixels.Count; i++)
            {
                var addy = sector.Pixels[i].PreComputedAddress;
                promises.Add(new ValueTask(new Ping().SendPingAsync(addy, 1)).AsTask());
            }

            await Task.WhenAll(promises);
        }
        catch (Exception e) { Console.WriteLine(e); }
    });
}

double ComputePixelVariance(List<Pixel> pixels)
{
    double average = pixels.Sum(x => x.Luminosity) / pixels.Count;
    double stdDev = 0;
    for (int i = 0; i < pixels.Count; i++)
        stdDev += Math.Pow(pixels[i].Luminosity - average, 2);
    stdDev = Math.Sqrt(stdDev / pixels.Count);
    return average / stdDev;
}

class Sector
{
    public int X, Y;
    public List<Pixel> Pixels = new List<Pixel>();
    public int IntegritySum;
    public double VariationSum;
}

struct Pixel
{
    public bool Valid
        => PreComputedAddress != default;

    public int X, Y;
    public byte R, G, B;
    public IPAddress PreComputedAddress;

    public byte Luminosity
        => (byte)(0.3f * R + 0.59f * G + 0.11f * B);
}