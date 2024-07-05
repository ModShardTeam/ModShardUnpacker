using System.Text;
using System.Text.RegularExpressions;
using dnlib.DotNet;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.Metadata;
using System.Drawing.Imaging;
using System.Drawing;

namespace ModShardUnpacker;
internal static class MainOperations
{
    public static void MainCommand(string name, string? outputFolder)
    {
        outputFolder ??= Path.Join(Environment.CurrentDirectory, Path.DirectorySeparatorChar.ToString(), "out");
        Console.WriteLine($"Decompiling {name} in {outputFolder}");
        
        DirectoryInfo dir = new(outputFolder);
        if (dir.Exists) dir.Delete(true);
        dir.Create();

        ModFile? mod = FileReader.Read(name, dir);
        if (mod != null)
        {
            Console.WriteLine($"Successfully reading {name}");
        }
        else
        {
            Console.WriteLine($"Failed reading {name}");
            return;
        }

        ModuleContext modCtx = ModuleDef.CreateModuleContext();
        ModuleDefMD module = ModuleDefMD.Load(mod.asm, modCtx);
        if (module.IsILOnly) {
            // This assembly has only IL code, and no native code (eg. it's a C# or VB assembly)
            module.Write(Path.Join(dir.FullName, Path.DirectorySeparatorChar.ToString(), $"{mod.name}.dll"));
        }
        else {
            // This assembly has native code (eg. C++/CLI)
            module.NativeWrite(Path.Join(dir.FullName, Path.DirectorySeparatorChar.ToString(), $"{mod.name}.dll"));
        }
        MemoryStream dllStream = new(mod.asm);
        PEFile pe = new($"Assembly.dll", dllStream);
        UniversalAssemblyResolver assemblyResolver = new(null, false, ".NETCoreApp,Version=v6.0");
        ICSharpCode.Decompiler.CSharp.CSharpDecompiler decompiler = new(pe, assemblyResolver, new DecompilerSettings());
        string code = decompiler.DecompileWholeModuleAsString();
        File.WriteAllText(Path.Join(dir.FullName, Path.DirectorySeparatorChar.ToString(), $"{mod.name}.cs"), code);
    }
}
internal class FileChunk 
{
    public string name;
    public int offset;
    public int length;
    public bool isTexture;
}
internal class ModFile
{
    public string name;
    public byte[] asm;
    public int FileOffset;
}
internal static class FileReader
{
    public static void GetVersion(FileStream fs, string nameMod)
    {
        Regex? reg = new("0([0-9])");
        byte pointBytes = 0x2E;
        byte zeroBytes = 0x30;
        byte nineBytes = 0x39;
        // the version number should be at least 24 bytes
        byte[] readbytes = Read(fs, 24);
        // resizing
        int size = 24;
        // i = 0 is a v
        for (int i = 1; i < 24; i++)
        {
            // either a point or in a range of [0-9]
            if (readbytes[i] != pointBytes && (readbytes[i] > nineBytes || readbytes[i] < zeroBytes))
            {
                size = i;
                break;
            }
        }

        if (size == 24)
        {
            Console.WriteLine("Version number seems ill formed");
        }

        // restarting the buffer
        fs.Seek(-24, SeekOrigin.Current);
        // reading the correct version
        byte[] versionbytes = Read(fs, size);

        string version = reg.Replace(Encoding.UTF8.GetString(versionbytes), "$1");
        Console.WriteLine($"Reading {nameMod} built with version {version}");
    }
    public static void ReadChunk(List<FileChunk> fileChunks, FileStream fs, bool isTexture)
    {
        int count = BitConverter.ToInt32(Read(fs, 4), 0);
        for (int i = 0; i < count; i++)
        {
            int len = BitConverter.ToInt32(Read(fs, 4));

            FileChunk chunk = new()
            {
                name = Encoding.UTF8.GetString(Read(fs, len)),
                offset = BitConverter.ToInt32(Read(fs, 4)),
                length = BitConverter.ToInt32(Read(fs, 4)),
                isTexture = isTexture
            };

            fileChunks.Add(chunk);
        }
    }
    public static ModFile? Read(string path, DirectoryInfo dir)
    {
        FileStream fs = new(path, FileMode.Open);
        List<FileChunk> fileChunks = new();

        ModFile mod = new()
        {
            name = fs.Name.Split("\\")[^1].Replace(".sml", "")
        };

        if (Encoding.UTF8.GetString(Read(fs, 4)) != "MSLM")
        {
            fs.Close();
            return null;
        }

        // read version
        GetVersion(fs, mod.name);

        // read textures
        ReadChunk(fileChunks, fs, true);
        
        // scripts
        ReadChunk(fileChunks, fs, false);

        // codes
        ReadChunk(fileChunks, fs, false);
        
        // assembly
        ReadChunk(fileChunks, fs, false);

        mod.FileOffset = (int)fs.Position;
        
        int fileCount = fileChunks.Count;
        if(fileCount > 0)
        {
            FileChunk? f = fileChunks[fileCount - 1];
            Read(fs, f.offset + f.length);
        }

        int count = BitConverter.ToInt32(Read(fs, 4), 0);
        mod.asm = Read(fs, count);

        foreach(FileChunk chunk in fileChunks)
        {
            fs.Position = mod.FileOffset + chunk.offset;
            byte[] fileStream = Read(fs, chunk.length);

            string chunkPath = Path.Join(dir.FullName, Path.DirectorySeparatorChar.ToString(), chunk.name);
            Console.WriteLine($"Exporting {chunkPath}");
            string? chunkDir = Path.GetDirectoryName(chunkPath);
            if (chunkDir != null && !Directory.Exists(chunkDir)) Directory.CreateDirectory(chunkDir);

            if (!chunk.isTexture)
            {
                if (fileStream.Length == 0) 
                {
                    File.WriteAllText(chunkPath, "");
                    continue;
                }
                if (fileStream[0] == 0xEF && fileStream[1] == 0xBB && fileStream[2] == 0xBF) fileStream = fileStream.Skip(3).ToArray();
                string text = Encoding.UTF8.GetString(fileStream);
                File.WriteAllText(chunkPath, text);
            }
            else
            {
                using Image image = Image.FromStream(new MemoryStream(fileStream));
                image.Save(chunkPath, ImageFormat.Png);
            }
        }

        fs.Close();
        return mod;
    }
    public static byte[] Read(FileStream fs, int length)
    {
        byte[] bytes = new byte[length];
        if(fs.Length - fs.Position < length)
        {
            fs.Close();
            throw new ArgumentException($"In FileReader.Read cannot read {length} bytes in the mod {fs.Name.Split("\\")[^1]} ");
        }
        fs.Read(bytes, 0, length);
        return bytes;
    }
}