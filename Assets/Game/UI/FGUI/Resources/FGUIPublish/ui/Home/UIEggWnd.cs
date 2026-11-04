/** This is an automatically generated class by FairyGUI. Please do not modify it. **/

using FairyGUI;
using FairyGUI.Utils;

namespace Home
{
    public partial class UIEggWnd : GComponent
    {
        public GGraph m_bg;
        public const string URL = "ui://l851vxyybxlunh";

        public static UIEggWnd CreateInstance()
        {
            return (UIEggWnd)UIPackage.CreateObject("Home", "EggWnd");
        }

        public override void ConstructFromXML(XML xml)
        {
            base.ConstructFromXML(xml);

            m_bg = (GGraph)GetChildAt(0);
        }
    }
}