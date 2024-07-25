using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PS3_XMB_Tools
{
    public class TaskbarIconUpdater
    {
        public void UpdateTaskbarIconWithTint(Window window, string iconPath, Color tint)
        {
            BitmapImage originalIcon = new BitmapImage(new Uri(iconPath));
            WriteableBitmap writeableBitmap = new WriteableBitmap(originalIcon);
            writeableBitmap.Lock();

            unsafe
            {
                IntPtr buffer = writeableBitmap.BackBuffer;
                int stride = writeableBitmap.BackBufferStride;
                byte tintStrength = 255;

                for (int y = 0; y < writeableBitmap.PixelHeight; y++)
                {
                    for (int x = 0; x < writeableBitmap.PixelWidth; x++)
                    {
                        int position = y * stride + x * 4;
                        byte* pixel = (byte*)buffer.ToPointer() + position;
                        byte alpha = pixel[3];
                        if (alpha > 0)
                        {
                            pixel[0] = (byte)((pixel[0] * (255 - tintStrength) + tint.B * tintStrength) / 255);
                            pixel[1] = (byte)((pixel[1] * (255 - tintStrength) + tint.G * tintStrength) / 255);
                            pixel[2] = (byte)((pixel[2] * (255 - tintStrength) + tint.R * tintStrength) / 255);
                        }
                    }
                }
            }

            writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, writeableBitmap.PixelWidth, writeableBitmap.PixelHeight));
            writeableBitmap.Unlock();
            window.Icon = writeableBitmap;
        }
    }
}