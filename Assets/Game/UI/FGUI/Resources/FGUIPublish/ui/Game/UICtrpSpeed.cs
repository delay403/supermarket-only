/** This is an automatically generated class by FairyGUI. Please do not modify it. **/

using FairyGUI;
using FairyGUI.Utils;

namespace Game
{
    public partial class UICtrpSpeed : GButton
    {
        public Controller m_StatusAd;
        public GLoader m_Fill;
        public GLoader m_Icon;
        public GTextField m_Label;
        public GTextField m_CountTime;
        public GLoader m_IconAd;
        public const string URL = "ui://6laopsucicg1nu";

        public static UICtrpSpeed CreateInstance()
        {
            return (UICtrpSpeed)UIPackage.CreateObject("Game", "CtrpSpeed");
        }

        public override void ConstructFromXML(XML xml)
        {
            base.ConstructFromXML(xml);

            m_StatusAd = GetControllerAt(0);
            m_Fill = (GLoader)GetChildAt(0);
            m_Icon = (GLoader)GetChildAt(1);
            m_Label = (GTextField)GetChildAt(2);
            m_CountTime = (GTextField)GetChildAt(3);
            m_IconAd = (GLoader)GetChildAt(4);
        }
    }
}