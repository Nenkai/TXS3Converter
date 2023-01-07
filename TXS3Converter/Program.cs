using System;
using System.Linq;
using System.IO;
using System.Text;

using PDTools.Files.Textures;
using PDTools.Files.Models;

using Syroot.BinaryData;
using System.Collections.Generic;

using CommandLine.Text;
using CommandLine;
using static PDTools.Files.Textures.TextureSet3;

namespace GTTools
{
    class Program
    {
        public static bool isBatchConvert = false;
        private static int processedFiles = 0;
        public static string currentFileName;

        static void Main(string[] args)
        {
            Console.WriteLine("Gran Turismo Texture Set (TXS3) / PDI Texture (PDI0) Converter");

            var p = Parser.Default.ParseArguments<ConvertToPngVerbs, ConvertToImgVerbs>(args);
            p.WithParsed<ConvertToPngVerbs>(ConvertToPng)
             .WithParsed<ConvertToImgVerbs>(ConvertToImg)
             .WithNotParsed(e => { });
        }

        public static void ConvertToPng(ConvertToPngVerbs verbs)
        {
            foreach (var file in verbs.InputPath)
            {
                if (!File.Exists(file) && !Directory.Exists(file))
                {
                    Console.WriteLine($"File does not exist: {file}");
                    continue;
                }

                FileAttributes attr = File.GetAttributes(file);
                if ((attr & FileAttributes.Directory) == FileAttributes.Directory)
                {
                    string[] files = Directory.GetFiles(file, "*.*", SearchOption.AllDirectories);
                    foreach (string f in files)
                    {
                        currentFileName = Path.GetFileName(f);
                        try
                        {
                            ConvertFileToPng(f, verbs.Format);
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
                    currentFileName = Path.GetFileName(file);
                    try
                    {
                        ConvertFileToPng(file, verbs.Format);
                        processedFiles++;
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($@"[!] Could not convert {currentFileName} : {e.Message}");
                    }
                }
            }

            Console.WriteLine($"Done, {processedFiles} files were converted.");
        }

        public static void ConvertToImg(ConvertToImgVerbs verbs)
        {
            TextureSet3 textureSet = new TextureSet3();

            foreach (var file in verbs.InputPath)
            {
                if (AddFileToTextureSet(textureSet, file, verbs))
                {
                    Console.WriteLine($"Failed to add file '{file}', aborting.");
                    return;
                }
            }

            textureSet.ConvertToTXS("test.img");
        }

        static bool _texConvExists = false;
        static void ConvertFileToPng(string path, TextureConsoleType consoleType)
        {
            currentFileName = Path.GetFileName(path);
            
            string magic = GetFileMagic(path);
            switch (magic)
            {
                case "TXS3":
                case "3SXT":
                    ProcessTextureSetFile(path, consoleType);
                    return;
                case "IDP0":
                    ProcessPDITexture(path);
                    return;
                case "MDL3":
                    ProcessModelSetFile(path);
                    return;
            }
        }

        public static bool AddFileToTextureSet(TextureSet3 textureSet, string path, ConvertToImgVerbs verbs)
        {
            if (verbs.Format != TextureConsoleType.PS3)
            {
                Console.WriteLine("Currently only PS3 TXS3 can be created.");
                return false;
            }

            // Convert if valid extension
            string ext = Path.GetExtension(path);
            if (ext.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
                ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
                ext.Equals(".bmp", StringComparison.OrdinalIgnoreCase))
            {
                if (!_texConvExists && !File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "texconv.exe")))
                {
                    Console.WriteLine("TexConv (image to DDS tool) is missing. Download it from https://github.com/microsoft/DirectXTex/releases and place it next to the tool.");
                    Environment.Exit(0);
                }
                _texConvExists = true;

                CELL_GCM_TEXTURE_FORMAT format = 0;
                if (verbs.CellFormat.Equals("DXT1"))
                    format = CELL_GCM_TEXTURE_FORMAT.CELL_GCM_TEXTURE_COMPRESSED_DXT1;
                else if (verbs.CellFormat.Equals("DXT3"))
                    format = CELL_GCM_TEXTURE_FORMAT.CELL_GCM_TEXTURE_COMPRESSED_DXT23;
                else if (verbs.CellFormat.Equals("DXT5"))
                    format = CELL_GCM_TEXTURE_FORMAT.CELL_GCM_TEXTURE_COMPRESSED_DXT45;
                else if (verbs.CellFormat.Equals("DXT10"))
                    format = CELL_GCM_TEXTURE_FORMAT.CELL_GCM_TEXTURE_A8R8G8B8;
                else
                {
                    Console.WriteLine("DXT format is invalid or not provided. must be DXT1/DXT3/DXT5/DXT10.");
                    return false;
                }

                var texture = new CellTexture();
                /*
                if (verbs.Swizzle)
                    (texture.TextureRenderInfo as PGLUCellTextureInfo).FormatBits &= ~CELL_GCM_TEXTURE_FORMAT.CELL_GCM_TEXTURE_LN; // Remove the default linear flag, set up swizzle
                */

                var added = texture.FromStandardImage(path, format);
                if (!added)
                    return false;

                textureSet.AddTexture(texture);


                return true;
            }
            else
            {
                Console.WriteLine($"Skipped {path}, no operation to be done");
                return true;
            }
        }

        static void ProcessTextureSetFile(string path, TextureConsoleType consoleType)
        {
            var txs = new TextureSet3();
            txs.FromFile(path, consoleType);
            txs.ConvertToPng(path);

            Console.WriteLine($"Converted {currentFileName} to png.");
        }

        static void ProcessPDITexture(string path)
        {
            var pdiTexture = new PDITexture();
            pdiTexture.FromFile(path);
            pdiTexture.TextureSet.ConvertToPng(path);

            Console.WriteLine($"Converted {currentFileName} to png.");
        }

        static void ProcessModelSetFile(string path)
        {
            using var fs = new FileStream(path, FileMode.Open);
            using var bs = new BinaryStream(fs);
            MDL3 set = MDL3.FromStream(bs);

            set.TextureSet.ConvertToPng(path);
        }

        static string GetFileMagic(string path)
        {
            using var fs = new FileStream(path, FileMode.Open);

            Span<byte> mBuf = stackalloc byte[4];
            fs.Read(mBuf);
            return Encoding.ASCII.GetString(mBuf);
        }
    }

    [Verb("convert-png", HelpText = "Converts any img to png.")]
    public class ConvertToPngVerbs
    {
        [Option('i', "input", Required = true, HelpText = "Input file texture or folder.")]
        public IEnumerable<string> InputPath { get; set; }

        [Option('f', "format", HelpText = "TXS format. Currently supported is PS3 & PS4. Defaults to PS3.")]
        public TextureConsoleType Format { get; set; } = TextureConsoleType.PS3;
    }

    [Verb("convert-img", HelpText = "Converts any image to .img.")]
    public class ConvertToImgVerbs
    {
        [Option('i', "input", Required = true, HelpText = "Input files for texture set")]
        public IEnumerable<string> InputPath { get; set; }

        [Option('f', "format", Required = true, HelpText = "TXS format. Currently supported is PS3. Valid options: PS3/PS4")]
        public TextureConsoleType Format { get; set; }

        [Option("pf", HelpText = "Pixel format when converting for PS3 - Valid options: DXT1/DXT3/DXT5/DXT10 ")]
        public string CellFormat { get; set; }
    }
}
