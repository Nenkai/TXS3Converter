using System;
using System.Linq;
using System.IO;
using System.Text;

using GTTools.Formats;

namespace GTTools
{
    class Program
    {
        public static bool isBatchConvert = false;
        private static int processedFiles = 0;
        public static string currentFileName;

        static void Main(string[] args)
        {
            Console.WriteLine("Gran Turismo TXS3 Converter");
            if (args.Length < 1)
            {
                Console.WriteLine("    Usage: <input_files>");
            }
            else
            {
                foreach (var arg in args)
                {
                    if (!File.Exists(arg) && !Directory.Exists(arg))
                    {
                        Console.WriteLine($"File does not exist: {arg}");
                        continue;
                    }

                    FileAttributes attr = File.GetAttributes(arg);
                    if ((attr & FileAttributes.Directory) == FileAttributes.Directory)
                    {
                        string[] files = Directory.GetFiles(arg, "*.*", SearchOption.TopDirectoryOnly);
                        if (files.Length > 1) isBatchConvert = true;
                        foreach (string f in files)
                        {
                            currentFileName = Path.GetFileName(f);
                            try
                            {
                                ProcessFile(f, args);
                                processedFiles++;
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine($@"[!] Could not convert {currentFileName} : {e.Message}");
                            }
                        }
                    }
                    else
                    {
                        if (args.Length > 1) 
                            isBatchConvert = true;

                        currentFileName = Path.GetFileName(arg);
                        try
                        {
                            ProcessFile(arg, args);
                            processedFiles++;
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine($@"[!] Could not convert {currentFileName} : {e.Message}");
                        }
                    }
                }
            }

            Console.WriteLine($"Done, {processedFiles} files were converted.");
        }

        static bool _texConvExists = false;
        static void ProcessFile(string path, string[] args)
        {
            currentFileName = Path.GetFileName(path);
            
            string magic = GetFileMagic(path);
            switch (magic)
            {
                case "TXS3":
                case "3SXT":
                    ProcessTXS3Texture(path);
                    break;
                case "MDL3":
                    ProcessMDL3Model(path);
                    break;
                default:
                    if (!_texConvExists && !File.Exists("texconv.exe"))
                    {
                        Console.WriteLine("TexConv (image to DDS tool) is missing. Download it from https://github.com/microsoft/DirectXTex/releases");
                        Environment.Exit(0);
                    }
                    _texConvExists = true;

                    TXS3.ImageFormat format = 0;
                    if (args.Contains("--DXT1"))
                        format = TXS3.ImageFormat.DXT1;
                    else if (args.Contains("--DXT3"))
                        format = TXS3.ImageFormat.DXT3;
                    else if (args.Contains("--DXT5"))
                        format = TXS3.ImageFormat.DXT5;
                    else if (args.Contains("--DXT10"))
                        format = TXS3.ImageFormat.DXT10;
                    else
                    {
                        Console.WriteLine("If you tried to convert an image to TXS, provide the corresponding image format argument at the end. (--DXT1/DXT3/DXT5/DXT10)");
                        Environment.Exit(0);
                    }

                    if (TXS3.ToTXS3File(path, format))
                        Console.WriteLine($"Converted {path} to TXS3");
                    else
                        Console.WriteLine($"Could not process {path}.");
                    return;
            }
        }

        static void ProcessTXS3Texture(string path)
        {
            var tex = TXS3.ParseFromFile(path);
            Console.WriteLine($"DDS Image format: {tex.Format}");

            string dir = Path.GetDirectoryName(path);
            string finalFileName = Path.GetFileName(path) + ".png";

            tex.SaveAsPng(Path.Combine(dir, finalFileName));
            Console.WriteLine($"Converted {currentFileName} to png.");
        }

        static void ProcessMDL3Model(string path)
        {
            MDL3.FromFile(path);
        }

        static string GetFileMagic(string path)
        {
            using var fs = new FileStream(path, FileMode.Open);

            Span<byte> mBuf = stackalloc byte[4];
            fs.Read(mBuf);
            return Encoding.ASCII.GetString(mBuf);
        }
    }
}
