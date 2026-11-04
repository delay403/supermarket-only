/** This is an automatically generated class by FairyGUI. Please do not modify it. **/

using FairyGUI;
using FairyGUI.Utils;

namespace Game
{
    public partial class UIMoney : GButton
    {
        public GLoader m_BG;
        public GTextField m_Label;
        public const string URL = "ui://6laopsucicg1qh";

        public static UIMoney CreateInstance()
        {
            return (UIMoney)UIPackage.CreateObject("Game", "Money");
        }

        public override void ConstructFromXML(XML xml)
        {
            base.ConstructFromXML(xml);

            m_BG = (GLoader)GetChildAt(0);
            m_Label = (GTextField)GetChildAt(2);
        }
    }
}