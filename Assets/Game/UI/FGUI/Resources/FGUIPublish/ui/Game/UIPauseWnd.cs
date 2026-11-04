/** This is an automatically generated class by FairyGUI. Please do not modify it. **/

using FairyGUI;
using FairyGUI.Utils;

namespace Game
{
    public partial class UIPauseWnd : GComponent
    {
        public GGraph m_bg;
        public const string URL = "ui://6laopsucn2cde8";

        public static UIPauseWnd CreateInstance()
        {
            return (UIPauseWnd)UIPackage.CreateObject("Game", "PauseWnd");
        }

        public override void ConstructFromXML(XML xml)
        {
            base.ConstructFromXML(xml);

            m_bg = (GGraph)GetChildAt(0);
        }
    }
}