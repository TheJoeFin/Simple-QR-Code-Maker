using System.Runtime.InteropServices;
using System.Text;
using Simple_QR_Code_Maker.Contracts.Services;

namespace Simple_QR_Code_Maker.Helpers;

public class RuntimeHelper
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetCurrentPackageFullName(ref int packageFullNameLength, StringBuilder? packageFullName);

    public static bool IsMSIX
    {
        get
        {
            int length = 0;

            return GetCurrentPackageFullName(ref length, null) != 15700L;
        }
    }
}

public sealed class RuntimeEnvironment : IRuntimeEnvironment
{
    public bool IsPackaged => RuntimeHelper.IsMSIX;

    public string LocalApplicationDataPath => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
}
