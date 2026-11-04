/** This is an automatically generated class by FairyGUI. Please do not modify it. **/

using FairyGUI;
using FairyGUI.Utils;

namespace Loading
{
    public partial class UILoading : GComponent
    {
        public Controller m_type;
        public Controller m_channel;
        public Controller m_age;
        public Controller m_underAge;
        public UIkaiping m_kp;
        public GProgressBar m_bar;
        public GButton m_startBtn;
        public UIhwLogin m_hwLogin;
        public GComponent m_underAge_2;
        public GButton m_zxBtn;
        public GButton m_ysBtn;
        public const string URL = "ui://69318iqykhm70";

        public static UILoading CreateInstance()
        {
            return (UILoading)UIPackage.CreateObject("Loading", "Loading");
        }

        public override void ConstructFromXML(XML xml)
        {
            base.ConstructFromXML(xml);

            m_type = GetControllerAt(0);
            m_channel = GetControllerAt(1);
            m_age = GetControllerAt(2);
            m_underAge = GetControllerAt(3);
            m_kp = (UIkaiping)GetChildAt(2);
            m_bar = (GProgressBar)GetChildAt(3);
            m_startBtn = (GButton)GetChildAt(4);
            m_hwLogin = (UIhwLogin)GetChildAt(5);
            m_underAge_2 = (GComponent)GetChildAt(7);
            m_zxBtn = (GButton)GetChildAt(8);
            m_ysBtn = (GButton)GetChildAt(9);
        }
    }
}