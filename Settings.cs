using System.Windows.Forms;
using Rage;

namespace Opticom
{
    internal class Settings
    {
        internal static Keys ToggleKey = Keys.F6;
        internal static InitializationFile iniFile;
        
        internal static void Initialize()
        {
            try
            {
                iniFile = new InitializationFile(@"Plugins/LSPDFR/Opticom.ini");
                iniFile.Create();
                ToggleKey = iniFile.ReadEnum("Keybinds", "ToggleKey", ToggleKey);
            }
            catch(System.Exception e)
            {
                string error = e.ToString();
                Game.LogTrivial("Opticom: ERROR IN 'Settings.cs, Initialize()': " + error);
                Game.DisplayNotification("Opticom: Error Occured");
            }
        }
    }
}