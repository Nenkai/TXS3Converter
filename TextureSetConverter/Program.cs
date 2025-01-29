using System;
using System.Linq;
using System.IO;
using System.Text;

using PDTools.Files.Textures.PS3;
using PDTools.Files.Textures;
using PDTools.Files.Models.PS3.ModelSet3;

using Syroot.BinaryData;
using System.Collections.Generic;

using CommandLine.Text;
using CommandLine;
using static PDTools.Files.Textures.TextureSet3;

namespace TextureSetConverter;

class Program
{
    public static bool isBatchConvert = false;
    private static int processedFiles = 0;
    public static string currentFileName;

    static void Main(string[] args)
    {
        Console.WriteLine("Gran Turismo Texture Set (TXS3) / PDI Texture (PDI0) Converter - 1.3.0");

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
                Console.WriteLine($"ERROR: File does not exist: {file}");
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
                        Console.WriteLine($@"ERROR: Could not convert {currentFileName} : {e.Message}");
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
                    Console.WriteLine($@"ERROR: Could not convert {currentFileName} : {e.Message}");
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
            if (!AddFileToTextureSet(textureSet, file, verbs))
            {
                Console.WriteLine($"ERROR: Failed to add file '{file}', aborting.");
                return;
            }
        }

        if (verbs.InputPath.Count() == 1)
        {
            textureSet.BuildTextureSetFile(Path.ChangeExtension(verbs.InputPath.First(), ".img"));
        }
        else
        {
            if (string.IsNullOrEmpty(verbs.OutputPath))
            {
                Console.WriteLine("ERROR: Please specify an output file name when building multiple textures into one texture set.");
                return;
            }

            Console.WriteLine("Building a texture set file with multiple textures is not yet supported.");
            return;
        }
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
                ProcessTextureSetToPng(path, consoleType);
                return;
            case "STRB":
                ProcessStrobeFile(path);
                return;
            case "IDP0":
                ConvertPDITextureToPng(path);
                return;
            case "MDL3":
            case "3LDM":
                ProcessModelSetFile(path);
                return;
        }
    }

    public static void ProcessStrobeFile(string path)
    {
        using var fs = new FileStream(path, FileMode.Open);
        fs.Position = 0xB0;
        int offset = fs.ReadInt32();
        int count = fs.ReadInt32();

        for (var i = 0; i < count; i++)
        {
            fs.Position = offset + (i * 0x10);
            int textureOffset = fs.ReadInt32();

            fs.Position = textureOffset;

            var texture = new TextureSet3();
            texture.FromStream(fs, TextureConsoleType.PS3);
            texture.ConvertToStandardFormat(path + $"_{i}.png");
        }
    }

    public static bool AddFileToTextureSet(TextureSet3 textureSet, string path, ConvertToImgVerbs verbs)
    {
        if (verbs.Format != TextureConsoleType.PS3)
        {
            Console.WriteLine("ERROR: Currently only PS3 TXS3 can be created.");
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
                Console.WriteLine("ERROR: DXT format is invalid or not provided. must be DXT1/DXT3/DXT5/DXT10.");
                return false;
            }

            var texture = new PGLUCellTextureInfo();
            texture.FormatBits = format;

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

    static void ProcessTextureSetToPng(string path, TextureConsoleType consoleType)
    {
        Console.WriteLine($"Converting {currentFileName} to png.");

        var txs = new TextureSet3();
        txs.FromFile(path, consoleType);
        txs.ConvertToStandardFormat(Path.ChangeExtension(path, ".png"));

    }

    static void ConvertPDITextureToPng(string path)
    {
        var pdiTexture = new PDITexture();
        pdiTexture.FromFile(path);
        pdiTexture.TextureSet.ConvertToStandardFormat(Path.ChangeExtension(path, ".png"));

        Console.WriteLine($"Converted {currentFileName} to png.");
    }

    static void ProcessModelSetFile(string path)
    {
        using var fs = new FileStream(path, FileMode.Open);
        using var bs = new BinaryStream(fs);
        ModelSet3 set = ModelSet3.FromStream(bs);

        set.TextureSet.ConvertToStandardFormat(path);
    }

    static string GetFileMagic(string path)
    {
        using var fs = new FileStream(path, FileMode.Open);

        Span<byte> mBuf = stackalloc byte[4];
        fs.ReadExactly(mBuf);
        return Encoding.ASCII.GetString(mBuf);
    }
}

[Verb("convert-png", HelpText = "Converts any img/mdl3/strb to png.")]
public class ConvertToPngVerbs
{
    [Option('i', "input", Required = true, HelpText = "Input file texture or folder.")]
    public IEnumerable<string> InputPath { get; set; }

    [Option('f', "format", HelpText = "TXS format. Currently supported is PS3/PS4/PSP. Defaults to PS3. Some textures may not work or output correctly.")]
    public TextureConsoleType Format { get; set; } = TextureConsoleType.PS3;
}

[Verb("convert-img", HelpText = "Converts any image to .img.")]
public class ConvertToImgVerbs
{
    [Option('i', "input", Required = true, HelpText = "Input files for texture set")]
    public IEnumerable<string> InputPath { get; set; }

    [Option('o', "output", HelpText = "Output texture set file. Not required if only building a texture set with one texture")]
    public string OutputPath { get; set; }

    [Option('f', "format", Required = true, HelpText = "TXS format. Currently supported is PS3. Valid options: PS3")]
    public TextureConsoleType Format { get; set; }

    [Option("pf", HelpText = "Pixel format when converting for PS3 - Valid options: DXT1/DXT3/DXT5/DXT10. Defaults to DXT5.")]
    public string CellFormat { get; set; } = "DXT5";
}
