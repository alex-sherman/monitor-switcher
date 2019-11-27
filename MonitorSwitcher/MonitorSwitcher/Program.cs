/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using System.IO;
using Newtonsoft.Json;

namespace MonitorSwitcherGUI
{
    class SaveFormat
    {
        public CCDWrapper.DisplayConfigPathInfo[] PathInfoList;
        public CCDWrapper.DisplayConfigModeInfo[] ModeInfoList;
    }
    public class MonitorSwitcher
    {

        static bool id_cmp(uint a, uint b) => (a & 0xffff) == (b & 0xffff);
        public static bool LoadDisplaySettings(string fileName)
        {
            if (!File.Exists(fileName))
            {
                Console.WriteLine("Failed to load display settings because file does not exist: " + fileName);
                return false;
            }
            SaveFormat save;
            using (StreamReader sr = new StreamReader(fileName))
            {
                save = JsonConvert.DeserializeObject<SaveFormat>(sr.ReadToEnd());
            }
            var pathInfoArray = save.PathInfoList;
            var modeInfoArray = save.ModeInfoList;

            CCDWrapper.DisplayConfigPathInfo[] pathInfoArrayCurrent = null;
            CCDWrapper.DisplayConfigModeInfo[] modeInfoArrayCurrent = null;
            bool statusCurrent = GetDisplaySettings(ref pathInfoArrayCurrent, ref modeInfoArrayCurrent, false);
            if (statusCurrent)
            {
                // For some reason the adapterID parameter changes upon system restart, all other parameters however, especially the ID remain constant.
                // We check the loaded settings against the current settings replacing the adapaterID with the other parameters
                for (int iPathInfo = 0; iPathInfo < pathInfoArray.Length; iPathInfo++)
                {
                    for (int iPathInfoCurrent = 0; iPathInfoCurrent < pathInfoArrayCurrent.Length; iPathInfoCurrent++)
                    {
                        if ((pathInfoArray[iPathInfo].sourceInfo.id == pathInfoArrayCurrent[iPathInfoCurrent].sourceInfo.id) &&
                            id_cmp(pathInfoArray[iPathInfo].targetInfo.id, pathInfoArrayCurrent[iPathInfoCurrent].targetInfo.id))
                        {
                            pathInfoArray[iPathInfo].targetInfo.id = pathInfoArrayCurrent[iPathInfoCurrent].targetInfo.id;
                            pathInfoArray[iPathInfo].sourceInfo.adapterId.LowPart = pathInfoArrayCurrent[iPathInfoCurrent].sourceInfo.adapterId.LowPart;
                            pathInfoArray[iPathInfo].targetInfo.adapterId.LowPart = pathInfoArrayCurrent[iPathInfoCurrent].targetInfo.adapterId.LowPart;
                            break;
                        }
                    }
                }

                // Same again for modeInfo, however we get the required adapterId information from the pathInfoArray
                for (int iModeInfo = 0; iModeInfo < modeInfoArray.Length; iModeInfo++)
                {
                    for (int iPathInfo = 0; iPathInfo < pathInfoArray.Length; iPathInfo++)
                    {
                        if (id_cmp(modeInfoArray[iModeInfo].id, pathInfoArray[iPathInfo].targetInfo.id) &&
                            (modeInfoArray[iModeInfo].infoType == CCDWrapper.DisplayConfigModeInfoType.Target))
                        {
                            modeInfoArray[iModeInfo].id = pathInfoArray[iPathInfo].targetInfo.id;
                            // We found target adapter id, now lets look for the source modeInfo and adapterID
                            for (int iModeInfoSource = 0; iModeInfoSource < modeInfoArray.Length; iModeInfoSource++)
                            {
                                if ((modeInfoArray[iModeInfoSource].id == pathInfoArray[iPathInfo].sourceInfo.id) &&
                                    (modeInfoArray[iModeInfoSource].adapterId.LowPart == modeInfoArray[iModeInfo].adapterId.LowPart) &&
                                    (modeInfoArray[iModeInfoSource].infoType == CCDWrapper.DisplayConfigModeInfoType.Source))
                                {
                                    modeInfoArray[iModeInfoSource].adapterId.LowPart = pathInfoArray[iPathInfo].sourceInfo.adapterId.LowPart;
                                    break;
                                }
                            }
                            modeInfoArray[iModeInfo].adapterId.LowPart = pathInfoArray[iPathInfo].targetInfo.adapterId.LowPart;
                            break;
                        }
                    }
                }
                // Set loaded display settings
                uint numPathArrayElements = (uint)pathInfoArray.Length;
                uint numModeInfoArrayElements = (uint)modeInfoArray.Length;
                long status = CCDWrapper.SetDisplayConfig(numPathArrayElements, pathInfoArray, numModeInfoArrayElements, modeInfoArray,
                                                          CCDWrapper.SdcFlags.Apply | CCDWrapper.SdcFlags.UseSuppliedDisplayConfig | CCDWrapper.SdcFlags.SaveToDatabase | CCDWrapper.SdcFlags.AllowChanges);
                if (status != 0)
                {
                    Console.WriteLine("Failed to set display settings, ERROR: " + status.ToString());

                    return false;
                }

                return true;
            }

            return false;
        }

        public static Boolean GetDisplaySettings(ref CCDWrapper.DisplayConfigPathInfo[] pathInfoArray, ref CCDWrapper.DisplayConfigModeInfo[] modeInfoArray, Boolean ActiveOnly)
        {
            uint numPathArrayElements;
            uint numModeInfoArrayElements;

            // query active paths from the current computer.
            CCDWrapper.QueryDisplayFlags queryFlags = CCDWrapper.QueryDisplayFlags.AllPaths;
            if (ActiveOnly)
            {
                queryFlags = CCDWrapper.QueryDisplayFlags.OnlyActivePaths;
            }

            var status = CCDWrapper.GetDisplayConfigBufferSizes(queryFlags, out numPathArrayElements, out numModeInfoArrayElements);
            if (status == 0)
            {
                pathInfoArray = new CCDWrapper.DisplayConfigPathInfo[numPathArrayElements];
                modeInfoArray = new CCDWrapper.DisplayConfigModeInfo[numModeInfoArrayElements];

                status = CCDWrapper.QueryDisplayConfig(queryFlags,
                                                       ref numPathArrayElements, pathInfoArray, ref numModeInfoArrayElements,
                                                       modeInfoArray, IntPtr.Zero);
                if (status == 0)
                {
                    return true;
                }
            }

            return false;
        }

        public static bool SaveDisplaySettings(String fileName)
        {
            var save = new SaveFormat();
            if (!GetDisplaySettings(ref save.PathInfoList, ref save.ModeInfoList, true))
                return false;
            using (StreamWriter sw = new StreamWriter(fileName))
                sw.Write(JsonConvert.SerializeObject(save));
            return true;
        }

        static void Main(string[] args)
        {
            bool validCommand = false;
            foreach (string iArg in args)
            {
                string[] argElements = iArg.Split(new char[] { ':' }, 2);

                switch (argElements[0].ToLower())
                {
                    case "-save":
                        SaveDisplaySettings(argElements[1]);
                        validCommand = true;
                        break;
                    case "-load":
                        LoadDisplaySettings(argElements[1]);
                        validCommand = true;
                        break;
                }
            }

            if (!validCommand)
            {
                Console.WriteLine("Monitor Profile Switcher command line utlility:\n");
                Console.WriteLine("Paremeters to MonitorSwitcher.exe:");
                Console.WriteLine("\t -save:{xmlfile} \t save the current monitor configuration to file");
                Console.WriteLine("\t -load:{xmlfile} \t load and apply monitor configuration from file");
                Console.WriteLine("");
                Console.WriteLine("Examples:");
                Console.WriteLine("\tMonitorSwitcher.exe -save:MyProfile.xml");
                Console.WriteLine("\tMonitorSwitcher.exe -load:MyProfile.xml");
                Console.ReadKey();
            }
        }
    }
}
