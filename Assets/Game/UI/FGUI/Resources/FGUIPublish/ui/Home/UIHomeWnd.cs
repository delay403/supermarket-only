/** This is an automatically generated class by FairyGUI. Please do not modify it. **/

using FairyGUI;
using FairyGUI.Utils;

namespace Home
{
    public partial class UIHomeWnd : GComponent
    {
        public GLoader m_bg;
        public GButton m_startBtn;
        public GLoader m_title;
        public Transition m_aircraftMove;
        public const string URL = "ui://l851vxyygeg30";

        public static UIHomeWnd CreateInstance()
        {
            return (UIHomeWnd)UIPackage.CreateObject("Home", "HomeWnd");
        }

        public override void ConstructFromXML(XML xml)
        {
            base.ConstructFromXML(xml);

            m_bg = (GLoader)GetChildAt(0);
            m_startBtn = (GButton)GetChildAt(1);
            m_title = (GLoader)GetChildAt(2);
            m_aircraftMove = GetTransitionAt(0);
        }
    }
}