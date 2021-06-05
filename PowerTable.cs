using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace ZenStates.Core
{
    public class PowerTable : INotifyPropertyChanged
    {
        private readonly PTDef tableDef;
        private uint[] table;
        public readonly uint dramBaseAddress;
        public readonly int tableSize;

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(PropertyChangedEventArgs eventArgs)
        {
            PropertyChanged?.Invoke(this, eventArgs);
        }

        protected bool SetProperty<T>(ref T storage, T value, PropertyChangedEventArgs args)
        {
            if (Equals(storage, value) || value == null)
                return false;

            storage = value;
            OnPropertyChanged(args);

            return true;
        }

        // Power table definition
        private struct PTDef
        {
            public int tableVersion;
            public int tableSize;
            public int offsetFclk;
            public int offsetUclk;
            public int offsetMclk;
            public int offsetVddcrSoc;
            public int offsetCldoVddp;
            public int offsetCldoVddgIod;
            public int offsetCldoVddgCcd;
            public int offsetCoresPower;
        }

        private class PowerTableDef : List<PTDef>
        {
            public void Add(int tableVersion, int tableSize, int offsetFclk, int offsetUclk, int offsetMclk,
                int offsetVddcrSoc, int offsetCldoVddp, int offsetCldoVddgIod, int offsetCldoVddgCcd, int offsetCoresPower)
            {
                Add(new PTDef
                {
                    tableVersion = tableVersion,
                    tableSize = tableSize,
                    offsetFclk = offsetFclk,
                    offsetUclk = offsetUclk,
                    offsetMclk = offsetMclk,
                    offsetVddcrSoc = offsetVddcrSoc,
                    offsetCldoVddp = offsetCldoVddp,
                    offsetCldoVddgIod = offsetCldoVddgIod,
                    offsetCldoVddgCcd = offsetCldoVddgCcd,
                    offsetCoresPower = offsetCoresPower
                });
            }
        }


        /// <summary>
        /// List of power table definitions for the different table versions found.
        /// If the sepcific detected version isn't found in the list, a generic one
        /// (usually the newest known) is selected.
        /// 
        /// Generic tables are defined with a faux version.
        /// APU faux versions start from 0x10
        /// CPU faux versions:
        /// Zen : 0x100
        /// Zen+: 0x101
        /// Zen2: 0x200
        /// Zen3: 0x300
        /// </summary>
        // version, size, FCLK, UCLK, MCLK, VDDCR_SOC, CLDO_VDDP, CLDO_VDDG_IOD, CLDO_VDDG_CCD, Cores Power Offset
        private static readonly PowerTableDef powerTables = new PowerTableDef
        {
            // Zen and Zen+ APU
            { 0x1E0001, 0x570, 0x460, 0x464, 0x468, 0x10C, 0xF8, -1, -1, -1 },
            { 0x1E0002, 0x570, 0x474, 0x478, 0x47C, 0x10C, 0xF8, -1, -1, -1 },
            { 0x1E0003, 0x610, 0x298, 0x29C, 0x2A0, 0x104, 0xF0, -1, -1, -1 },
            { 0x1E0004, 0x610, 0x298, 0x29C, 0x2A0, 0x104, 0xF0, -1, -1, -1 },
            // Generic (latest known)
            { 0x000010, 0x610, 0x298, 0x29C, 0x2A0, 0x104, 0xF0, -1, -1, -1 },

            // FireFlight
            { 0x260001, 0x610, 0x28, 0x2C, 0x30, 0x10, -1, -1, -1, -1 },

            // Zen2 APU (Renoir)
            { 0x370000, 0x79C, 0x4B4, 0x4B8, 0x4BC, 0x190, 0x72C, -1, -1, -1 },
            { 0x370001, 0x88C, 0x5A4, 0x5A8, 0x5AC, 0x190, 0x81C, -1, -1, -1 },
            { 0x370002, 0x894, 0x5AC, 0x5B0, 0x5B4, 0x198, 0x824, -1, -1, -1 },
            { 0x370003, 0x8B4, 0x5CC, 0x5D0, 0x5D4, 0x198, 0x844, -1, -1, -1 },
            // 0x370004 missing
            { 0x370005, 0x8D0, 0x5E8, 0x5EC, 0x5F0, 0x198, 0x86C, -1, -1, -1 },
            // Generic Zen2 APU (latest known)
            { 0x000011, 0x8D0, 0x5E8, 0x5EC, 0x5F0, 0x198, 0x86C, -1, -1, -1 },

            // Zen3 APU (Cezanne)
            { 0x400001, 0x8D0, 0x624, 0x628, 0x62C, 0x19C, 0x89C, -1, -1, -1 },
            { 0x400002, 0x8D0, 0x63C, 0x640, 0x644, 0x19C, 0x8B4, -1, -1, -1 },            
            { 0x400003, 0x944, 0x660, 0x664, 0x668, 0x19C, 0x8D0, -1, -1, -1 },
            { 0x400004, 0x948, 0x664, 0x668, 0x66C, 0x19C, 0x8D4, -1, -1, -1 },
            { 0x400005, 0x948, 0x664, 0x668, 0x66C, 0x19C, 0x8D4, -1, -1, -1 },
            // Generic Zen3 APU
            { 0x000012, 0x948, 0x664, 0x668, 0x66C, 0x19C, 0x8D4, -1, -1, -1 },

            // Zen CPU
            { 0x000100, 0x7E4, 0x84, 0x84, 0x84, 0x68, 0x44, -1, -1, -1 },

            // Zen+ CPU
            { 0x000101, 0x7E4, 0x84, 0x84, 0x84, 0x60, 0x3C, -1, -1, -1 },

            // Zen2 CPU combined versions
            // version from 0x240000 to 0x240900 ?
            { 0x000200, 0x7E4, 0xB0, 0xB8, 0xBC, 0xA4, 0x1E4, 0x1E8, -1, -1 },
            // version from 0x240001 to 0x240901 ?
            // version from 0x240002 to 0x240902 ?
            // version from 0x240004 to 0x240904 ?
            { 0x000202, 0x7E4, 0xBC, 0xC4, 0xC8, 0xB0, 0x1F0, 0x1F4, -1, -1 },
            // version from 0x240003 to 0x240903 ?
            // Generic Zen2 CPU (latest known)
            { 0x000203, 0x7E4, 0xC0, 0xC8, 0xCC, 0xB4, 0x1F4, 0x1F8, -1, 0x24C },

            // Zen3 CPU
            // This table is found in some early beta bioses for Vermeer (SMU version 56.27.00)
            { 0x2D0903, 0x7E4, 0xBC, 0xC4, 0xC8, 0xB0, 0x220, 0x224, -1, -1 },
            // Generic Zen 3 CPU (latest known)
            { 0x000300, 0x7E4, 0xC0, 0xC8, 0xCC, 0xB4, 0x224, 0x228, 0x22C, -1 },
        };

        private PTDef GetDefByVersion(uint version)
        {
            return powerTables.Find(x => x.tableVersion == version);
        }

        private PTDef GetDefaultTableDef(uint tableVersion, SMU.SmuType smutype)
        {
            uint version = 0;

            switch (smutype)
            {
                case SMU.SmuType.TYPE_CPU0:
                    version = 0x100;
                    break;

                case SMU.SmuType.TYPE_CPU1:
                    version = 0x101;
                    break;

                case SMU.SmuType.TYPE_CPU2:
                    uint temp = tableVersion & 0x7;
                    if (temp == 0)
                        version = 0x200;
                    else if (temp == 1 || temp == 2 || temp == 4)
                        version = 0x202;
                    else
                        version = 0x203;
                    break;

                case SMU.SmuType.TYPE_CPU3:
                    version = 0x300;
                    break;

                case SMU.SmuType.TYPE_APU0:
                    version = 0x10;
                    break;

                case SMU.SmuType.TYPE_APU1:
                    if ((tableVersion >> 16) == 0x37)
                        version = 0x11;
                    else
                        version = 0x12;
                    break;

                default:
                    break;
            }

            return GetDefByVersion(version);
        }

        private PTDef GetPowerTableDef(uint tableVersion, SMU.SmuType smutype)
        {
            PTDef temp = GetDefByVersion(tableVersion);
            if (temp.tableSize != 0)
                return temp;
            else
                return GetDefaultTableDef(tableVersion, smutype);

            throw new Exception("Power Table not supported");
        }

        public PowerTable(uint version, SMU.SmuType smutype, uint dramAddress)
        {
            if (dramAddress == 0)
                throw new ApplicationException("Could not get DRAM base address.");

            SmuType = smutype;
            TableVersion = version;
            tableDef = GetPowerTableDef(version, smutype);
            tableSize = tableDef.tableSize;
            table = new uint[tableSize / 4];
            dramBaseAddress = dramAddress;
        }

        private uint GetDiscreteValue(uint[] pt, int index)
        {
            if (index > 0 && index < tableSize)
                return pt[index / 4];
            return 0;
        }

        private void ParseTable(uint[] pt)
        {
            if (pt == null)
                return;

            float bclkCorrection = 1.00f;
            byte[] bytes;

            // Compensate for lack of BCLK detection, based on configuredClockSpeed
            if (ConfiguredClockSpeed > 0 && MemRatio > 0)
                bclkCorrection = ConfiguredClockSpeed / (MemRatio * 200);

            try
            {
                bytes = BitConverter.GetBytes(GetDiscreteValue(pt, tableDef.offsetMclk));
                float mclkFreq = BitConverter.ToSingle(bytes, 0);
                MCLK = mclkFreq * bclkCorrection;
            }
            catch { }

            try
            {
                bytes = BitConverter.GetBytes(GetDiscreteValue(pt, tableDef.offsetFclk));
                float fclkFreq = BitConverter.ToSingle(bytes, 0);
                FCLK = fclkFreq * bclkCorrection;
            }
            catch { }

            try
            {
                bytes = BitConverter.GetBytes(GetDiscreteValue(pt, tableDef.offsetUclk));
                float uclkFreq = BitConverter.ToSingle(bytes, 0);
                UCLK = uclkFreq * bclkCorrection;
            }
            catch { }

            try
            {
                bytes = BitConverter.GetBytes(GetDiscreteValue(pt, tableDef.offsetVddcrSoc));
                VDDCR_SOC = BitConverter.ToSingle(bytes, 0);
            }
            catch { }

            try
            {
                bytes = BitConverter.GetBytes(GetDiscreteValue(pt, tableDef.offsetCldoVddp));
                CLDO_VDDP = BitConverter.ToSingle(bytes, 0);
            }
            catch { }

            try
            {
                bytes = BitConverter.GetBytes(GetDiscreteValue(pt, tableDef.offsetCldoVddgIod));
                CLDO_VDDG_IOD = BitConverter.ToSingle(bytes, 0);
            }
            catch { }

            try
            {
                bytes = BitConverter.GetBytes(GetDiscreteValue(pt, tableDef.offsetCldoVddgCcd));
                CLDO_VDDG_CCD = BitConverter.ToSingle(bytes, 0);
            }
            catch { }
            
            // Test
            /*if (tableDef.offsetCoresPower > 0)
            {
                Console.WriteLine();
                for (int i = 0; i < 16; i++)
                {
                    bytes = BitConverter.GetBytes(GetDiscreteValue(pt, tableDef.offsetCoresPower + i * 4));
                    float power = BitConverter.ToSingle(bytes, 0);
                    string status = power > 0 ? "Enabled" : "Disabled";
                    Console.WriteLine($"Core{i}: {power} -> {status}");
                }
            }*/
        }

        // Static one-time properties
        public static SMU.SmuType SmuType { get; protected set; } = SMU.SmuType.TYPE_UNSUPPORTED;
        public static uint TableVersion { get; protected set; } = 0;
        public float ConfiguredClockSpeed { get; set; } = 0;
        public float MemRatio { get; set; } = 0;

        // Dynamic properties
        public uint[] Table
        {
            get => table;
            set
            {
                if (value != null)
                {
                    table = value;
                    ParseTable(value);
                }
            }
        }

        float fclk;
        public float FCLK
        {
            get => fclk;
            set => SetProperty(ref fclk, value, InternalEventArgsCache.FCLK);
        }

        float mclk;
        public float MCLK
        {
            get => mclk;
            set => SetProperty(ref mclk, value, InternalEventArgsCache.MCLK);
        }

        float uclk;
        public float UCLK
        {
            get => uclk;
            set => SetProperty(ref uclk, value, InternalEventArgsCache.UCLK);
        }

        float vddcr_soc;
        public float VDDCR_SOC
        {
            get => vddcr_soc;
            set => SetProperty(ref vddcr_soc, value, InternalEventArgsCache.VDDCR_SOC);
        }

        float cldo_vddp;
        public float CLDO_VDDP
        {
            get => cldo_vddp;
            set => SetProperty(ref cldo_vddp, value, InternalEventArgsCache.CLDO_VDDP);
        }

        float cldo_vddg_iod;
        public float CLDO_VDDG_IOD
        {
            get => cldo_vddg_iod;
            set => SetProperty(ref cldo_vddg_iod, value, InternalEventArgsCache.CLDO_VDDG_IOD);
        }

        float cldo_vddg_ccd;

        public float CLDO_VDDG_CCD
        {
            get => cldo_vddg_ccd;
            set => SetProperty(ref cldo_vddg_ccd, value, InternalEventArgsCache.CLDO_VDDG_CCD);
        }
    }

    internal static class InternalEventArgsCache
    {
        internal static PropertyChangedEventArgs FCLK = new PropertyChangedEventArgs("FCLK");
        internal static PropertyChangedEventArgs MCLK = new PropertyChangedEventArgs("MCLK");
        internal static PropertyChangedEventArgs UCLK = new PropertyChangedEventArgs("UCLK");

        internal static PropertyChangedEventArgs VDDCR_SOC = new PropertyChangedEventArgs("VDDCR_SOC");
        internal static PropertyChangedEventArgs CLDO_VDDP = new PropertyChangedEventArgs("CLDO_VDDP");
        internal static PropertyChangedEventArgs CLDO_VDDG_IOD = new PropertyChangedEventArgs("CLDO_VDDG_IOD");
        internal static PropertyChangedEventArgs CLDO_VDDG_CCD = new PropertyChangedEventArgs("CLDO_VDDG_CCD");
    }
}
