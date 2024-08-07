﻿using System;
using System.Collections.Generic;
using System.Management;

namespace ZenStates.Core.DRAM
{
    public class MemoryConfig
    {
        private const int DRAM_TYPE_BIT_MASK = 0x1;

        private const uint DRAM_TYPE_REG_ADDR = 0x50100;

        private const int MAX_CHANNELS = 0x8;

        private readonly uint ChannelsPerDimm;

        private readonly Cpu cpu;

        public struct Channel
        {
            public bool Enabled;

            public uint Offset;
        }

        public struct TimingDef
        {
            public string Name;

            public int HiBit;

            public int LoBit;
        }

        public enum MemType
        {
            DDR4 = 0,
            DDR5 = 1,
            LPDDR5 = 2,
        }

        public enum CapacityUnit
        {
            B = 0,
            KB = 1,
            MB = 2,
            GB = 3,
        }

        public MemType Type { get; protected set; }

        public Capacity TotalCapacity { get; protected set; }

        public List<KeyValuePair<uint, BaseDramTimings>> Timings { get; protected set; }

        public List<Channel> Channels { get; protected set; }

        public List<MemoryModule> Modules { get; protected set; }

        public MemoryConfig(Cpu cpuInstance)
        {
            cpu = cpuInstance;

            Type = (MemType)(cpu.ReadDword(0 | DRAM_TYPE_REG_ADDR) & DRAM_TYPE_BIT_MASK);

            ChannelsPerDimm = 1; // Type == MemType.DDR5 ? 2u : 1u;

            Channels = new List<Channel>();

            Modules = new List<MemoryModule>();

            ReadModulesInfo();

            ReadChannels();

            Timings = new List<KeyValuePair<uint, BaseDramTimings>>();

            foreach (MemoryModule module in Modules)
            {
                if (Type == MemType.DDR4)
                    Timings.Add(new KeyValuePair<uint, BaseDramTimings>(module.DctOffset, new Ddr4Timings(cpu)));
                else if (Type == MemType.DDR5)
                    Timings.Add(new KeyValuePair<uint, BaseDramTimings>(module.DctOffset, new Ddr5Timings(cpu)));

                ReadTimings(module.DctOffset);
            }
        }

        public void ReadTimings(uint offset = 0)
        {
            foreach (var item in Timings)
            {
                if (item.Key == offset)
                {
                    item.Value.Read(offset);
                    break;
                }
            }
        }

        private void ReadModulesInfo()
        {
            using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("select * from Win32_PhysicalMemory"))
            {
                bool connected = false;

                try
                {
                    WMI.Connect(@"root\cimv2");

                    connected = true;

                    foreach (var qo in searcher.Get())
                    {
                        var capacity = 0UL;
                        var clockSpeed = 0U;
                        var partNumber = "N/A";
                        var bankLabel = "";
                        var manufacturer = "";
                        var deviceLocator = "";

                        var queryObject = (ManagementObject)qo;

                        var temp = WMI.TryGetProperty(queryObject, "Capacity");
                        if (temp != null) capacity = (ulong)temp;

                        temp = WMI.TryGetProperty(queryObject, "ConfiguredClockSpeed");
                        if (temp != null) clockSpeed = (uint)temp;

                        temp = WMI.TryGetProperty(queryObject, "partNumber");
                        if (temp != null) partNumber = (string)temp;

                        temp = WMI.TryGetProperty(queryObject, "BankLabel");
                        if (temp != null) bankLabel = (string)temp;

                        temp = WMI.TryGetProperty(queryObject, "Manufacturer");
                        if (temp != null) manufacturer = (string)temp;

                        temp = WMI.TryGetProperty(queryObject, "DeviceLocator");
                        if (temp != null) deviceLocator = (string)temp;

                        Modules.Add(new MemoryModule(partNumber.Trim(), bankLabel.Trim(), manufacturer.Trim(),
                            deviceLocator, capacity, clockSpeed));

                        //string bl = bankLabel.Length > 0 ? new string(bankLabel.Where(char.IsDigit).ToArray()) : "";
                        //string dl = deviceLocator.Length > 0 ? new string(deviceLocator.Where(char.IsDigit).ToArray()) : "";

                        //comboBoxPartNumber.Items.Add($"#{bl}: {partNumber}");
                        //comboBoxPartNumber.SelectedIndex = 0;
                    }
                }
                catch (Exception ex)
                {
                    throw new ApplicationException(connected ? @"Failed to get installed memory parameters." : $@"{ex.Message}");
                }

                if (Modules?.Count > 0)
                {
                    ulong totalCapacity = 0UL;
                    foreach (MemoryModule module in Modules)
                    {
                        totalCapacity += module.Capacity.SizeInBytes;
                    }
                    TotalCapacity = new Capacity(totalCapacity);
                }
            }
        }

        private void ReadChannels()
        {
            int dimmIndex = 0;
            uint dimmsPerChannel = 1;

            // Get the offset by probing the UMC0 to UMC7
            // It appears that offsets 0x80 and 0x84 are DIMM config registers
            // When a DIMM is DR, bit 0 is set to 1
            // 0x50000
            // offset 0, bit 0 when set to 1 means DIMM1 is installed
            // offset 8, bit 0 when set to 1 means DIMM2 is installed
            for (uint i = 0; i < MAX_CHANNELS * ChannelsPerDimm; i += ChannelsPerDimm)
            {
                try
                {
                    uint offset = i << 20;
                    bool channel = Utils.GetBits(cpu.ReadDword(offset | 0x50DF0), 19, 1) == 0;
                    bool dimm1 = Utils.GetBits(cpu.ReadDword(offset | 0x50000), 0, 1) == 1;
                    bool dimm2 = Utils.GetBits(cpu.ReadDword(offset | 0x50008), 0, 1) == 1;
                    bool enabled = channel && (dimm1 || dimm2);

                    Channels.Add(new Channel()
                    {
                        Enabled = enabled,
                        Offset = offset,
                    });

                    if (enabled)
                    {
                        if (dimm1)
                        {
                            MemoryModule module = Modules[dimmIndex++];
                            module.Slot = $"{Convert.ToChar(i / ChannelsPerDimm + 65)}1";
                            module.DctOffset = offset;
                            module.Rank = (MemRank)Utils.GetBits(cpu.ReadDword(offset | 0x50080), 0, 1);
                        }

                        if (dimm2)
                        {
                            MemoryModule module = Modules[dimmIndex++];
                            module.Slot = $"{Convert.ToChar(i / ChannelsPerDimm + 65)}2";
                            module.DctOffset = offset;
                            module.Rank = (MemRank)Utils.GetBits(cpu.ReadDword(offset | 0x50084), 0, 1);
                        }
                    }
                }
                catch
                {
                    // do nothing
                }
            }
        }
    }
}
