using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Buffers.Binary;
using Xabe.FFmpeg.Downloader;

if (args.Length < 1 || args.Contains("-h") || args.Contains("--help")) { Zz9(); return args.Length < 1 ? 1 : 0; }
string p1 = args[0];
if (!File.Exists(p1)) { Console.Error.WriteLine($"Input file not found: {p1}"); return 1; }
string p2 = args.Length >= 2 && !args[1].StartsWith('-') ? args[1] : Path.Combine(Path.GetDirectoryName(Path.GetFullPath(p1)) ?? ".", Path.GetFileNameWithoutExtension(p1) + "_dotcode");
Directory.CreateDirectory(p2);
var p3 = new A();
byte[] p4 = File.ReadAllBytes(p1);
int p5 = I.M11(p3);
if (p4.Length == 0) { Console.Error.WriteLine("Input file is empty."); return 1; }
var p6 = new List<(int, int)>();
int p7 = 0;
while (p7 < p4.Length) { int zl = Math.Min(p5, p4.Length - p7); p6.Add((p7, zl)); p7 += zl; }
int p8 = p6.Count;
if (p8 > ushort.MaxValue) { Console.Error.WriteLine($"File requires {p8} frames, which exceeds the maximum of {ushort.MaxValue}. Use a smaller file."); return 1; }
uint p9 = (uint)Random.Shared.Next();
Console.WriteLine($"Input:        {p1} ({p4.Length:N0} bytes)");
Console.WriteLine($"Canvas:       {p3.M1}x{p3.M2}, {p3.M9}x{p3.M10} dot grid, {p3.M18} data dots/frame");
Console.WriteLine($"Capacity:     {p5} bytes/frame");
Console.WriteLine($"Frames:       {p8}");
Console.WriteLine($"Session ID:   {p9}");
Console.WriteLine($"Output dir:   {p2}");
Console.WriteLine();
bool p10 = args.Contains("--no-video");
double p11 = 1.5;
int p12 = 4;
double p13 = 6.0;
for (int zi = 0; zi < args.Length; zi++)
{
    if (args[zi] == "--hold" && zi + 1 < args.Length) p11 = double.Parse(args[++zi]);
    if (args[zi] == "--loops" && zi + 1 < args.Length) p12 = int.Parse(args[++zi]);
    if (args[zi] == "--lead-in" && zi + 1 < args.Length) p13 = double.Parse(args[++zi]);
}
int p15 = Math.Max(3, p8.ToString().Length);
var p16 = new List<string>();
for (int zi = 0; zi < p6.Count; zi++)
{
    var (zo, zl2) = p6[zi];
    var zh = new H { M1 = p9, M2 = (ushort)zi, M3 = (ushort)p8, M4 = (uint)p4.Length, M5 = (ushort)zl2 };
    var zc = I.M14(p3, zh, p4.AsSpan(zo, zl2));
    using var zb = J.M1(p3, zc);
    string zp = Path.Combine(p2, $"frame_{(zi + 1).ToString().PadLeft(p15, '0')}_of_{p8}.png");
    zb.Save(zp, ImageFormat.Png);
    p16.Add(zp);
    Console.WriteLine($"  wrote {zp}");
}
Console.WriteLine();
if (!p10)
{
    string zv = Path.Combine(p2, "playback.mp4");
    Console.WriteLine($"Building playback video ({p13}s black lead-in, {p11}s/frame, {p12} loop(s))...");
    string zf = await K.M3();
    string zli = Path.Combine(p2, "leadin_black.png");
    using (var zbk = new Bitmap(p3.M1, p3.M2))
    using (var zg = Graphics.FromImage(zbk))
    {
        zg.Clear(B.M1);
        zbk.Save(zli, ImageFormat.Png);
    }
    await Zz1(p16, zv, p11, p12, zli, p13, zf);
    Console.WriteLine($"Wrote {zv}");
    Console.WriteLine();
    Console.WriteLine($"The video opens with {p13}s of plain black (not a real frame) so a video player's UI");
    Console.WriteLine("chrome has time to auto-hide before real content starts. Start recording during the");
    Console.WriteLine("black lead-in, then let it play through.");
    Console.WriteLine();
    Console.WriteLine("Play that file full-screen (native 1920x1080, no scaling/motion-smoothing) and record");
    Console.WriteLine("it with a phone camera, then run the matching decoder on the recorded video.");
}
else
{
    Console.WriteLine($"Done. Display each frame full-screen in order and photograph it with your phone,");
    Console.WriteLine($"then decode the {p8} photo(s) to reconstruct the file.");
}
return 0;

static async Task Zz1(List<string> zf, string zo, double zh, int zl, string zli, double zls, string ze)
{
    string zp = Path.Combine(Path.GetTempPath(), "dotcode_concat_" + Guid.NewGuid().ToString("N") + ".txt");
    var zn = new List<string> { $"file '{zli.Replace('\\', '/')}'", $"duration {zls.ToString(System.Globalization.CultureInfo.InvariantCulture)}" };
    for (int i = 0; i < zl; i++)
    {
        foreach (var q in zf)
        {
            zn.Add($"file '{q.Replace('\\', '/')}'");
            zn.Add($"duration {zh.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
        }
    }
    zn.Add($"file '{zf[^1].Replace('\\', '/')}'");
    File.WriteAllLines(zp, zn);
    try
    {
        var zs = new System.Diagnostics.ProcessStartInfo { FileName = ze, UseShellExecute = false, RedirectStandardError = true, CreateNoWindow = true };
        zs.ArgumentList.Add("-y"); zs.ArgumentList.Add("-f"); zs.ArgumentList.Add("concat"); zs.ArgumentList.Add("-safe"); zs.ArgumentList.Add("0");
        zs.ArgumentList.Add("-i"); zs.ArgumentList.Add(zp); zs.ArgumentList.Add("-r"); zs.ArgumentList.Add("30"); zs.ArgumentList.Add("-pix_fmt");
        zs.ArgumentList.Add("yuv420p"); zs.ArgumentList.Add("-c:v"); zs.ArgumentList.Add("libx264"); zs.ArgumentList.Add("-crf"); zs.ArgumentList.Add("15");
        zs.ArgumentList.Add("-preset"); zs.ArgumentList.Add("slow"); zs.ArgumentList.Add(zo);
        using var zpr = System.Diagnostics.Process.Start(zs)!;
        var zt = zpr.StandardError.ReadToEndAsync();
        await zpr.WaitForExitAsync();
        string ztr = await zt;
        if (zpr.ExitCode != 0) throw new InvalidOperationException($"ffmpeg exited with code {zpr.ExitCode}:\n{ztr}");
    }
    finally { File.Delete(zp); }
}

static void Zz9()
{
    Console.WriteLine("DotCodeEncoder - encode a file as colored-dot frames sized for a 1920x1080 screen");
    Console.WriteLine();
    Console.WriteLine("Usage: DotCodeEncoder <inputFile> [outputDir] [--hold seconds] [--loops n] [--lead-in seconds] [--no-video]");
    Console.WriteLine();
    Console.WriteLine("  inputFile   File to encode.");
    Console.WriteLine("  outputDir   Directory to write frame_*.png images to (default: <input>_dotcode next to the input file).");
    Console.WriteLine("  --hold      Seconds to hold each frame on screen in the generated video (default 1.5).");
    Console.WriteLine("  --loops     How many times to loop the full frame sequence in the video (default 4).");
    Console.WriteLine("  --lead-in   Seconds of plain black video before the first real frame (default 6).");
    Console.WriteLine("  --no-video  Skip building playback.mp4; only write the individual frame PNGs.");
}

sealed class A
{
    public int M1 { get; }
    public int M2 { get; }
    public int M3 { get; }
    public int M4 { get; }
    public int M5 { get; }
    public int M6 { get; }
    public int M7 { get; }
    public int M8 { get; }
    public int M9 { get; }
    public int M10 { get; }
    private readonly HashSet<(int, int)> Mf1;
    private readonly HashSet<(int, int)> Mf2;
    private readonly List<(int, int, int)> Mf3;
    private readonly List<(int, int)> Mf4;

    public A(int a1 = 1920, int a2 = 1080, int a3 = 20, int a4 = 12, int a5 = 12, int a6 = 15, int a7 = 12, int a8 = 3)
    {
        M1 = a1; M2 = a2; M3 = a3; M4 = a4; M5 = a5; M6 = a6; M7 = a7; M8 = a8;
        if (a6 >= a3) throw new ArgumentException("Border thickness must leave room for a quiet zone inside the margin.");
        M9 = (M1 - 2 * M3) / M4;
        M10 = (M2 - 2 * M3) / M4;
        if (M9 < a7 * 3 || M10 < a7 * 3) throw new ArgumentException("Canvas too small for the requested pitch/margins.");
        Mf1 = Zm1();
        Mf2 = Zm2();
        Mf3 = Zm3();
        Mf4 = Zm4();
    }

    public PointF M15(int c, int r) => new(M3 + M4 / 2f + c * M4, M3 + M4 / 2f + r * M4);
    public IReadOnlyList<(int, int, int)> M16 => Mf3;
    public IReadOnlyList<(int, int)> M17 => Mf4;
    public int M18 => Mf4.Count;

    private HashSet<(int, int)> Zm1()
    {
        var s = new HashSet<(int, int)>();
        for (int c = 0; c < M7; c++) for (int r = 0; r < M7; r++) s.Add((c, r));
        return s;
    }

    private const int Zc1 = 3;

    private HashSet<(int, int)> Zm2()
    {
        var s = new HashSet<(int, int)>();
        foreach (var (c, r) in Mf1)
            for (int dc = -Zc1; dc <= Zc1; dc++)
                for (int dr = -Zc1; dr <= Zc1; dr++)
                {
                    int cc = c + dc, rr = r + dr;
                    if (cc >= 0 && cc < M9 && rr >= 0 && rr < M10) s.Add((cc, rr));
                }
        return s;
    }

    private List<(int, int, int)> Zm3()
    {
        var l = new List<(int, int, int)>();
        int r = M7, s0 = M7, e0 = M9, ci = 0, rp = 0;
        for (int c = s0; c < e0; c++)
        {
            if (Mf2.Contains((c, r))) continue;
            l.Add((c, r, ci));
            rp++;
            if (rp >= M8) { rp = 0; ci = (ci + 1) % B.M6; }
        }
        return l;
    }

    private List<(int, int)> Zm4()
    {
        var rz = new HashSet<(int, int)>(Mf2);
        foreach (var (c, r, _) in Mf3) rz.Add((c, r));
        var l = new List<(int, int)>();
        for (int r = 0; r < M10; r++) for (int c = 0; c < M9; c++) if (!rz.Contains((c, r))) l.Add((c, r));
        return l;
    }

    public (int, int) M24 => (0, 0);
}

static class B
{
    public static readonly Color M1 = Color.Black;
    public static readonly Color M2 = Color.FromArgb(230, 0, 230);
    public static readonly Color M3 = Color.White;
    public static readonly Color[] M4 = [Color.White, Color.FromArgb(255, 40, 40), Color.FromArgb(40, 220, 40), Color.FromArgb(60, 100, 255)];
    public const int M5 = 2;
    public const int M6 = 4;
    public static Color M7(int s) => M4[s];
}

static class C
{
    private static readonly uint[] T = Zt();
    private static uint[] Zt()
    {
        var t = new uint[256];
        for (uint i = 0; i < 256; i++) { uint c = i; for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1; t[i] = c; }
        return t;
    }
    public static uint M3(ReadOnlySpan<byte> d)
    {
        uint c = 0xFFFFFFFF;
        foreach (byte b in d) c = T[(c ^ b) & 0xFF] ^ (c >> 8);
        return c ^ 0xFFFFFFFF;
    }
}

static class D
{
    private const int Fs = 256;
    private const int Pp = 0x11d;
    private static readonly byte[] Ex = new byte[512];
    private static readonly byte[] Lg = new byte[Fs];
    static D()
    {
        int x = 1;
        for (int i = 0; i < 255; i++) { Ex[i] = (byte)x; Lg[x] = (byte)i; x <<= 1; if (x >= Fs) x ^= Pp; }
        for (int i = 255; i < 512; i++) Ex[i] = Ex[i - 255];
    }
    public static byte M5(byte a, byte b) => a == 0 || b == 0 ? (byte)0 : Ex[Lg[a] + Lg[b]];
    public static byte M6(byte a, int p)
    {
        if (a == 0) return p == 0 ? (byte)1 : (byte)0;
        int e = ((Lg[a] * p) % 255 + 255) % 255;
        return Ex[e];
    }
}

static class E
{
    public static byte[] M1(byte[] p, byte[] q)
    {
        var r = new byte[p.Length + q.Length - 1];
        for (int i = 0; i < p.Length; i++)
        {
            if (p[i] == 0) continue;
            for (int j = 0; j < q.Length; j++) r[i + j] ^= D.M5(p[i], q[j]);
        }
        return r;
    }
}

static class F
{
    public const int M1 = 255;
    public static byte[] M2(int n)
    {
        byte[] g = [1];
        for (int i = 0; i < n; i++) g = E.M1(g, [1, D.M6(2, i)]);
        return g;
    }
    public static byte[] M3(ReadOnlySpan<byte> d, int n)
    {
        if (d.Length + n > M1) throw new ArgumentException($"Codeword too long: {d.Length + n} > {M1}");
        var gp = M2(n);
        var buf = new byte[d.Length + n];
        d.CopyTo(buf);
        for (int i = 0; i < d.Length; i++)
        {
            byte co = buf[i];
            if (co == 0) continue;
            for (int j = 0; j < gp.Length; j++) buf[i + j] ^= D.M5(gp[j], co);
        }
        var cw = new byte[d.Length + n];
        d.CopyTo(cw);
        Array.Copy(buf, d.Length, cw, d.Length, n);
        return cw;
    }
}

static class G
{
    public static int[] M1(ReadOnlySpan<byte> b)
    {
        var s = new int[b.Length * 4];
        for (int i = 0; i < b.Length; i++)
        {
            byte v = b[i];
            s[i * 4 + 0] = (v >> 6) & 0b11; s[i * 4 + 1] = (v >> 4) & 0b11; s[i * 4 + 2] = (v >> 2) & 0b11; s[i * 4 + 3] = v & 0b11;
        }
        return s;
    }
}

sealed class H
{
    public required uint M1 { get; init; }
    public required ushort M2 { get; init; }
    public required ushort M3 { get; init; }
    public required uint M4 { get; init; }
    public required ushort M5 { get; init; }
}

static class I
{
    public const byte M1 = (byte)'D';
    public const byte M2 = (byte)'C';
    public const byte M3 = 2;
    public const int M4 = 21;
    public const int M5 = 64;
    public static int M6(A l) => l.M18 * B.M5 / 8;
    public static List<int> M7(A l, int n = M5)
    {
        var s = new List<int>();
        int rem = M6(l);
        while (rem >= n + 1) { int bc = Math.Min(F.M1, rem); s.Add(bc - n); rem -= bc; }
        return s;
    }
    public static int M8(A l, int n = M5) => M7(l, n).Sum();
    public static int M9(A l, int n = M5) => M7(l, n).Sum(s => s + n);
    public static int M10(A l, int n = M5) => M9(l, n) * 4;
    public static int M11(A l, int n = M5) => M8(l, n) - M4;
    public static int[][] M12(A l, int n = M5)
    {
        var bs = M7(l, n);
        var cn = bs.Select(s => (s + n) * 4).ToArray();
        var mp = cn.Select(c => new int[c]).ToArray();
        int mc = cn.Length == 0 ? 0 : cn.Max();
        int ph = 0;
        for (int o = 0; o < mc; o++) for (int b = 0; b < cn.Length; b++) if (o < cn[b]) mp[b][o] = ph++;
        return mp;
    }
    private static int M13(List<int> bs, int need, out int cum)
    {
        cum = 0; int c = 0;
        foreach (var b in bs) { c++; cum += b; if (cum >= need) return c; }
        return c;
    }
    public static byte[] M14(A l, H h, ReadOnlySpan<byte> pl, int n = M5)
    {
        var bs = M7(l, n);
        int ov = M4;
        int need = ov + pl.Length;
        int bn = M13(bs, need, out int cap);
        if (cap < need) throw new ArgumentException($"Payload too large for frame: {pl.Length} bytes, capacity {M8(l, n) - ov}.");
        var da = new byte[cap];
        var sp = da.AsSpan();
        sp[0] = M1; sp[1] = M2; sp[2] = M3;
        BinaryPrimitives.WriteUInt32BigEndian(sp[3..7], h.M1);
        BinaryPrimitives.WriteUInt16BigEndian(sp[7..9], h.M2);
        BinaryPrimitives.WriteUInt16BigEndian(sp[9..11], h.M3);
        BinaryPrimitives.WriteUInt32BigEndian(sp[11..15], h.M4);
        BinaryPrimitives.WriteUInt16BigEndian(sp[15..17], (ushort)pl.Length);
        uint cr = C.M3(pl);
        BinaryPrimitives.WriteUInt32BigEndian(sp[17..21], cr);
        pl.CopyTo(sp[M4..]);
        var cw = new byte[bs.Take(bn).Sum(s => s + n)];
        int dof = 0, cof = 0;
        for (int b = 0; b < bn; b++)
        {
            var bl = F.M3(da.AsSpan(dof, bs[b]), n);
            bl.CopyTo(cw.AsSpan(cof));
            dof += bs[b]; cof += bl.Length;
        }
        return cw;
    }
}

static class J
{
    public static Bitmap M1(A l, byte[] cw)
    {
        int mx = I.M10(l);
        var sy = G.M1(cw);
        if (sy.Length > mx) throw new ArgumentException($"Codeword produces {sy.Length} symbols, exceeding this layout's capacity of {mx}.");
        var bm = new Bitmap(l.M1, l.M2, PixelFormat.Format24bppRgb);
        using (var g = Graphics.FromImage(bm))
        {
            g.SmoothingMode = SmoothingMode.None;
            using (var bb = new SolidBrush(B.M2)) g.FillRectangle(bb, 0, 0, l.M1, l.M2);
            using (var gb = new SolidBrush(B.M1)) g.FillRectangle(gb, l.M6, l.M6, l.M1 - 2 * l.M6, l.M2 - 2 * l.M6);
            {
                var (c0, r0) = l.M24;
                float x0 = l.M3 + c0 * l.M4, y0 = l.M3 + r0 * l.M4, sz = l.M7 * l.M4;
                using var br = new SolidBrush(B.M3);
                g.FillRectangle(br, x0, y0, sz, sz);
            }
            foreach (var (c, r, ci) in l.M16) M2(g, l.M15(c, r), l.M5, B.M7(ci));
            var dc = l.M17;
            var bs = I.M7(l);
            var im = I.M12(l);
            int i = 0;
            for (int b = 0; b < bs.Count && i < sy.Length; b++)
            {
                int bsc = im[b].Length;
                for (int o = 0; o < bsc && i < sy.Length; o++, i++)
                {
                    int ph = im[b][o];
                    var (c, r) = dc[ph];
                    M2(g, l.M15(c, r), l.M5, B.M7(sy[i]));
                }
            }
        }
        return bm;
    }
    private static void M2(Graphics g, PointF ce, int sz, Color co)
    {
        using var br = new SolidBrush(co);
        g.FillRectangle(br, ce.X - sz / 2f, ce.Y - sz / 2f, sz, sz);
    }
}

static class K
{
    public static string M1 { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DotCode", "ffmpeg-bin");
    public static string M2(string d) => Path.Combine(d, "ffmpeg.exe");
    public static async Task<string> M3(string? d = null)
    {
        d ??= M1;
        string e = M2(d);
        if (!File.Exists(e))
        {
            Console.WriteLine("[ffmpeg] Downloading ffmpeg (first run only)...");
            Directory.CreateDirectory(d);
            await FFmpegDownloader.GetLatestVersion(FFmpegVersion.Full, d);
        }
        return e;
    }
}
