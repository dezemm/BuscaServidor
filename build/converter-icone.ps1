$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
Add-Type -AssemblyName System.Drawing
if (-not ('AppIconBuilder' -as [type])) {
    Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Collections.Generic;
public static class AppIconBuilder {
    public static void Convert(string sourcePath, string targetPath) {
        int[] sizes = {16, 24, 32, 48, 64, 128, 256};
        var images = new List<byte[]>();
        using (var source = Image.FromFile(sourcePath)) {
            foreach (int size in sizes) {
                using (var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb))
                using (var graphics = Graphics.FromImage(bitmap))
                using (var png = new MemoryStream()) {
                    graphics.Clear(Color.Transparent);
                    graphics.CompositingMode = CompositingMode.SourceCopy;
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    float scale = Math.Min((float)size/source.Width, (float)size/source.Height);
                    int width = Math.Max(1, (int)Math.Round(source.Width*scale));
                    int height = Math.Max(1, (int)Math.Round(source.Height*scale));
                    graphics.DrawImage(source, new Rectangle((size-width)/2, (size-height)/2, width, height));
                    bitmap.Save(png, ImageFormat.Png); images.Add(png.ToArray());
                }
            }
        }
        using (var file = File.Create(targetPath))
        using (var writer = new BinaryWriter(file)) {
            writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)sizes.Length);
            int offset = 6 + 16*sizes.Length;
            for (int i=0; i<sizes.Length; i++) {
                writer.Write((byte)(sizes[i]==256 ? 0 : sizes[i])); writer.Write((byte)(sizes[i]==256 ? 0 : sizes[i]));
                writer.Write((byte)0); writer.Write((byte)0); writer.Write((ushort)1); writer.Write((ushort)32);
                writer.Write(images[i].Length); writer.Write(offset); offset += images[i].Length;
            }
            foreach (var png in images) writer.Write(png);
        }
    }
}
'@
}
$assets = Join-Path $taskRoot 'src\assets'
New-Item -ItemType Directory -Path $assets -Force | Out-Null
[AppIconBuilder]::Convert((Join-Path $taskRoot 'icone.png'), (Join-Path $assets 'icone.ico'))
