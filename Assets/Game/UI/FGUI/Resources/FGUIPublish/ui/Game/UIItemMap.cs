/** This is an automatically generated class by FairyGUI. Please do not modify it. **/

using FairyGUI;
using FairyGUI.Utils;

namespace Game
{
    public partial class UIItemMap : GButton
    {
        public GTextField m_Label;
        public const string URL = "ui://6laopsucicg1qv";

        public static UIItemMap CreateInstance()
        {
            return (UIItemMap)UIPackage.CreateObject("Game", "ItemMap");
        }

        public override void ConstructFromXML(XML xml)
        {
            base.ConstructFromXML(xml);

            m_Label = (GTextField)GetChildAt(3);
        }
    }
}