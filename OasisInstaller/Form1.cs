using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace OasisInstaller
{
    public partial class Form1 : Form
    {
        public DirectoryInfo subnauticaDirectory;

        public Form1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            var folderBrowserDialog = new FolderBrowserDialog();
            folderBrowserDialog.Description = "Browse for Subnautica Directory.";
            
            if(folderBrowserDialog.ShowDialog() == DialogResult.OK)
            {
                var path = folderBrowserDialog.SelectedPath;
                var directoryInfo = new DirectoryInfo(path);

                foreach(var file in directoryInfo.GetFiles())
                {
                    if(file.Name.Contains("Subnautica") && file.Extension == ".exe")
                    {
                        subnauticaDirectory = directoryInfo;
                        textBox1.Text = path;

                        return;
                    }
                }

                MessageBox.Show("Please select a valid Subnautica directory!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (subnauticaDirectory == null)
            {
                MessageBox.Show("Please select a valid Subnautica directory first!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var managedDirectory = new DirectoryInfo(Path.Combine(subnauticaDirectory.FullName, "Subnautica_Data/Managed/"));

            if (managedDirectory == null)
            {
                MessageBox.Show("Please select a valid Subnautica directory!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var assemblyPath = Path.Combine(managedDirectory.FullName + "/Assembly-CSharp.dll");
            var backupPath = Path.Combine(managedDirectory.FullName, "/Assembly-CSharp.backup");

            if (patched(assemblyPath))
            {
                MessageBox.Show("Oasis Mod Loader is already injected!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var installerFile = new FileInfo("OasisModLoader.dll");
            var smlHelperFile = new FileInfo("SMLHelper.dll");
            var harmonyFile = new FileInfo("0Harmony.dll");

            if(!File.Exists(Path.Combine(managedDirectory.FullName, "OasisModLoader.dll")))
                installerFile = installerFile.CopyTo(Path.Combine(managedDirectory.FullName, "OasisModLoader.dll"));
            if (!File.Exists(Path.Combine(managedDirectory.FullName, "SMLHelper.dll")))
                smlHelperFile = smlHelperFile.CopyTo(Path.Combine(managedDirectory.FullName, "SMLHelper.dll"));
            if (!File.Exists(Path.Combine(managedDirectory.FullName, "0Harmony.dll")))
                harmonyFile = harmonyFile.CopyTo(Path.Combine(managedDirectory.FullName, "0Harmony.dll"));

            var resolver = new DefaultAssemblyResolver();
            resolver.AddSearchDirectory(managedDirectory.FullName);
            var assembly = AssemblyDefinition.ReadAssembly(assemblyPath, new ReaderParameters { AssemblyResolver = resolver });

            if (File.Exists(backupPath))
                File.Delete(backupPath);

            //assembly.Write(backupPath);

            var installer = AssemblyDefinition.ReadAssembly(installerFile.FullName);
            var onGameStart = installer.MainModule.GetType("OasisModLoader.Main").Methods.Single(x => x.Name == "OnGameStart");

            var type = assembly.MainModule.GetType("GameInput");
            var method = type.Methods.Single(x => x.Name == "Awake");

            method.Body.GetILProcessor().InsertBefore(method.Body.Instructions[0], Instruction.Create(OpCodes.Call, method.Module.ImportReference(onGameStart)));

            assembly.Write(assemblyPath + ".mod");
            assembly.Dispose();
            File.Move(assemblyPath, assemblyPath + ".backup");
            File.Move(assemblyPath + ".mod", assemblyPath);

            if (!Directory.Exists(Path.Combine(subnauticaDirectory.FullName, "Subnautica_Data/SMods")))
                Directory.CreateDirectory(Path.Combine(subnauticaDirectory.FullName, "Subnautica_Data/SMods"));

            MessageBox.Show("Successfully installed Oasis Mod Loader!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
        }

        private bool patched(string assemblyCsharp)
        {
            var game = AssemblyDefinition.ReadAssembly(assemblyCsharp);

            var type = game.MainModule.GetType("GameInput");
            var method = type.Methods.First(x => x.Name == "Awake");

            foreach (var instruction in method.Body.Instructions)
            {
                if (instruction.OpCode.Equals(OpCodes.Call) && instruction.Operand.ToString().Equals("System.Void OasisModLoader.Main::OnGameStart()"))
                {
                    return true;
                }
            }

            game.Dispose();

            return false;
        }
    }
}
