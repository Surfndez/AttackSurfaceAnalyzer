﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using AttackSurfaceAnalyzer.Objects;
using AttackSurfaceAnalyzer.Utils;
using Microsoft.Win32;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AttackSurfaceAnalyzer.Collectors
{
    /// <summary>
    /// Collects data from the local registry on Windows systems.
    /// </summary>
    public class RegistryCollector : BaseCollector
    {
        private List<RegistryHive> Hives;
        private HashSet<string> roots;
        private HashSet<RegistryKey> _keys;
        private HashSet<RegistryObject> _values;

        private static readonly List<RegistryHive> DefaultHives = new List<RegistryHive>()
        {
            RegistryHive.ClassesRoot, RegistryHive.CurrentConfig, RegistryHive.CurrentUser, RegistryHive.LocalMachine, RegistryHive.Users
        };

        private Action<RegistryObject> customCrawlHandler = null;

        public RegistryCollector(string RunId) : this(RunId, DefaultHives, null) { }

        public RegistryCollector(string RunId, List<RegistryHive> Hives) : this(RunId, Hives, null) { }

        public RegistryCollector(string RunId, List<RegistryHive> Hives, Action<RegistryObject> customHandler)
        {
            this.RunId = RunId;
            this.Hives = Hives;
            this.roots = new HashSet<string>();
            this._keys = new HashSet<RegistryKey>();
            this._values = new HashSet<RegistryObject>();
            this.customCrawlHandler = customHandler;
        }

        public void AddRoot(string root)
        {
            this.roots.Add(root);
        }

        public void ClearRoots()
        {
            this.roots.Clear();
        }

        public override bool CanRunOnPlatform()
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        }

        public override void ExecuteInternal()
        {
            Parallel.ForEach(Hives,
                (hive =>
                {
                    Log.Debug("Starting " + hive.ToString());
                    if (!Filter.IsFiltered(AsaHelpers.GetPlatformString(), "Scan", "Registry", "Hive", "Include", hive.ToString()) && Filter.IsFiltered(AsaHelpers.GetPlatformString(), "Scan", "Registry", "Hive", "Exclude", hive.ToString(), out Regex Capturer))
                    {
                        Log.Debug("{0} '{1}' {2} '{3}'.", Strings.Get("ExcludingHive"), hive.ToString(), Strings.Get("DueToFilter"), Capturer.ToString());
                        return;
                    }

                    Filter.IsFiltered(AsaHelpers.GetPlatformString(), "Scan", "Registry", "Key", "Exclude", hive.ToString());
                    var registryInfoEnumerable = RegistryWalker.WalkHive(hive);
                    Parallel.ForEach(registryInfoEnumerable,
                        (registryObject =>
                        {
                            try
                            {
                                DatabaseManager.Write(registryObject, RunId);
                            }
                            catch (InvalidOperationException e)
                            {
                                Log.Debug(e, JsonConvert.SerializeObject(registryObject) + " invalid op exept");
                            }
                        }));
                }));
        }
    }
}