/** This is an automatically generated class by FairyGUI. Please do not modify it. **/

using FairyGUI;
using FairyGUI.Utils;

namespace Setting
{
    public partial class UISettingWnd : GComponent
    {
        public UIcheckBtn m_sound;
        public UIcheckBtn m_music;
        public GLoader m_text;
        public const string URL = "ui://el0ns6e9elmh0";

        public static UISettingWnd CreateInstance()
        {
            return (UISettingWnd)UIPackage.CreateObject("Setting", "SettingWnd");
        }

        public override void ConstructFromXML(XML xml)
        {
            base.ConstructFromXML(xml);

            m_sound = (UIcheckBtn)GetChildAt(3);
            m_music = (UIcheckBtn)GetChildAt(5);
            m_text = (GLoader)GetChildAt(7);
        }
    }
}