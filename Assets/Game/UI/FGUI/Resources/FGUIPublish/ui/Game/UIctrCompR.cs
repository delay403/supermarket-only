/** This is an automatically generated class by FairyGUI. Please do not modify it. **/

using FairyGUI;
using FairyGUI.Utils;

namespace Game
{
    public partial class UIctrCompR : GComponent
    {
        public GLoader m_Base;
        public GLoader m_wheel;
        public const string URL = "ui://6laopsucualrnj";

        public static UIctrCompR CreateInstance()
        {
            return (UIctrCompR)UIPackage.CreateObject("Game", "ctrCompR");
        }

        public override void ConstructFromXML(XML xml)
        {
            base.ConstructFromXML(xml);

            m_Base = (GLoader)GetChildAt(0);
            m_wheel = (GLoader)GetChildAt(1);
        }
    }
}