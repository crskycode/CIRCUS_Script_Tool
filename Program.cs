using System;
using System.IO;
using System.Text;

namespace CIRCUS_MES
{
    class Program
    {
        static void Main(string[] args)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            if (args.Length != 3)
            {
                Console.WriteLine("CIRCUS MES Tool");
                Console.WriteLine("  -- Created by Crsky");
                Console.WriteLine("Usage:");
                Console.WriteLine("  Export text     : ScriptTool -e shift_jis [file|folder]");
                Console.WriteLine("  Rebuild script  : ScriptTool -b shift_jis [file|folder]");
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey();
                return;
            }

            string mode = args[0];
            string encoding = args[1];
            string path = Path.GetFullPath(args[2]);

            switch (mode)
            {
                case "-e":
                {
                    void ExportText(string filePath)
                    {
                        Console.WriteLine($"Exporting text from {Path.GetFileName(filePath)}");

                        try
                        {
                            Script script = new Script();
                            script.Load(filePath);
                            script.ExportText(Path.ChangeExtension(filePath, "txt"), encoding);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e.Message);
                        }
                    }

                    if (Utility.PathIsFolder(path))
                    {
                        foreach (var item in Directory.EnumerateFiles(path, "*.mes"))
                        {
                            ExportText(item);
                        }
                    }
                    else
                    {
                        ExportText(path);
                    }

                    break;
                }
                case "-b":
                {
                    void RebuildScript(string filePath)
                    {
                        Console.WriteLine($"Rebuilding script {Path.GetFileName(filePath)}");

                        try
                        {
                            string textFilePath = Path.ChangeExtension(filePath, "txt");
                            string newFilePath = Path.GetDirectoryName(filePath) + @"\rebuild\" + Path.GetFileName(filePath);
                            Directory.CreateDirectory(Path.GetDirectoryName(newFilePath));
                            Script script = new Script();
                            script.Load(filePath);
                            script.ImportText(textFilePath, encoding);
                            script.Save(newFilePath);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e.Message);
                        }
                    }

                    if (Utility.PathIsFolder(path))
                    {
                        foreach (var item in Directory.EnumerateFiles(path, "*.mes"))
                        {
                            RebuildScript(item);
                        }
                    }
                    else
                    {
                        RebuildScript(path);
                    }

                    break;
                }
            }
        }
    }
}
