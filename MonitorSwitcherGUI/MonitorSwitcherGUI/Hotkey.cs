using System;
using System.Windows.Forms;
using System.ComponentModel;
using System.Xml.Serialization;
using System.Runtime.InteropServices;
using System.Collections.Generic;

// Based on https://bloggablea.wordpress.com/2007/05/01/global-hotkeys-with-net/
namespace MonitorSwitcherGUI
{

    [StructLayout(LayoutKind.Sequential)]
    public class Hotkey
    {
        public Boolean Ctrl;
        public Boolean Alt;
        public Boolean Shift;
        public Boolean RemoveKey;
        public Keys Key;
        public String profileName;

        public HotkeyCtrl hotkeyCtrl;

        public Hotkey()
        {
            hotkeyCtrl = new HotkeyCtrl();
            RemoveKey = false;
        }

        public void RegisterHotkey(MonitorSwitcherGUI parent)
        {
            hotkeyCtrl.Pressed += parent.KeyHook_KeyUp;
            hotkeyCtrl.Register(parent, this);
        }

        public void UnregisterHotkey(MonitorSwitcherGUI parent)
        {
            hotkeyCtrl.Pressed -= parent.KeyHook_KeyUp;
            if (hotkeyCtrl.Registered)
                hotkeyCtrl.Unregister();
        }

        public void AssignFromKeyEventArgs(KeyEventArgs keyEvents)
        {
            Ctrl = keyEvents.Control;
            Alt = keyEvents.Alt;
            Shift = keyEvents.Shift;
            Key = keyEvents.KeyCode;
        }

        public override string ToString()
        {
            List<string> keys = new List<string>();

            if (Ctrl == true)
            {
                keys.Add("CTRL");
            }

            if (Alt == true)
            {
                keys.Add("ALT");
            }

            if (Shift == true)
            {
                keys.Add("SHIFT");
            }

            switch (Key)
            {
                case Keys.ControlKey:
                case Keys.Alt:
                case Keys.ShiftKey:
                case Keys.Menu:
                    break;
                default:
                    keys.Add(Key.ToString()
                        .Replace("Oem", string.Empty)
                        );
                    break;
            }

            return string.Join(" + ", keys);
        }
    }
}
