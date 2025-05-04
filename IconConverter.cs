using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media.Imaging;

namespace DockZero
{
    public static class IconConverter
    {
        public static void ConvertPngsToIco()
        {
            string[] sizes = { "16", "32", "48", "256" };
            string[] pngFiles = sizes.Select(size => Path.Combine("icons", $"icon{size}.png")).ToArray();
            string icoPath = Path.Combine("icons", "icon32.ico");

            using (var iconWriter = new FileStream(icoPath, FileMode.Create))
            {
                // Write ICO header
                iconWriter.Write(new byte[] { 0, 0, 1, 0, (byte)sizes.Length, 0 }, 0, 6);

                // Calculate offsets and write directory entries
                int offset = 6 + (16 * sizes.Length);
                foreach (string pngFile in pngFiles)
                {
                    using (var pngStream = new FileStream(pngFile, FileMode.Open))
                    {
                        using (var bitmap = new Bitmap(pngStream))
                        {
                            int width = bitmap.Width;
                            int height = bitmap.Height;
                            int size = (int)pngStream.Length;

                            // Write directory entry
                            iconWriter.WriteByte((byte)width);
                            iconWriter.WriteByte((byte)height);
                            iconWriter.WriteByte(0); // color palette
                            iconWriter.WriteByte(0); // reserved
                            iconWriter.WriteByte(1); // color planes
                            iconWriter.WriteByte(32); // bits per pixel
                            WriteInt32(iconWriter, size);
                            WriteInt32(iconWriter, offset);

                            offset += size;
                        }
                    }
                }

                // Write image data
                foreach (string pngFile in pngFiles)
                {
                    using (var pngStream = new FileStream(pngFile, FileMode.Open))
                    {
                        pngStream.CopyTo(iconWriter);
                    }
                }
            }
        }

        private static void WriteInt32(Stream stream, int value)
        {
            stream.WriteByte((byte)(value & 0xFF));
            stream.WriteByte((byte)((value >> 8) & 0xFF));
            stream.WriteByte((byte)((value >> 16) & 0xFF));
            stream.WriteByte((byte)((value >> 24) & 0xFF));
        }
    }
} 