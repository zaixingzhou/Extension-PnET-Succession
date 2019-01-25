﻿//  Copyright 2007-2016 Portland State University
//  Author: Austen Ruzicka (and Robert Scheller)

using Landis.Core;
using Landis.SpatialModeling;
using Edu.Wisc.Forest.Flel.Util;
using System.IO;

namespace Landis.Extension.Succession.BiomassPnET
{
    /// <summary>
    /// Methods to read maps in lieu of spin-up
    /// </summary>
    public static class MapReader
    {
        public static void ReadWoodyDebrisFromMap(string path)
        {
            IInputRaster<DoublePixel> map = MakeDoubleMap(path);

            using (map)
            {
                DoublePixel pixel = map.BufferPixel;
                foreach (Site site in PlugIn.ModelCore.Landscape.AllSites)
                {
                    map.ReadBufferPixel();
                    int mapValue = (int)pixel.MapCode.Value;
                    if (site.IsActive)
                    {
                        if (mapValue < 0 || mapValue > 100000)
                            throw new InputValueException(mapValue.ToString(),
                                                          "Down dead value {0} is not between {1:0.0} and {2:0.0}. Site_Row={3:0}, Site_Column={4:0}",
                                                          mapValue, 0, 100000, site.Location.Row, site.Location.Column);
                        PlugIn.WoodyDebris[site].InitialMass = mapValue;
                        PlugIn.WoodyDebris[site].Mass = mapValue;
                    }
                }
            }
        }

        public static void ReadLitterFromMap(string path)
        {
            IInputRaster<DoublePixel> map = MakeDoubleMap(path);

            using (map)
            {
                DoublePixel pixel = map.BufferPixel;
                foreach (Site site in PlugIn.ModelCore.Landscape.AllSites)
                {
                    map.ReadBufferPixel();
                    int mapValue = (int)pixel.MapCode.Value;
                    if (site.IsActive)
                    {
                        if (mapValue < 0 || mapValue > 300)
                            throw new InputValueException(mapValue.ToString(),
                                                          "Soil depth value {0} is not between {1:0.0} and {2:0.0}. Site_Row={3:0}, Site_Column={4:0}",
                                                          mapValue, 0, 300, site.Location.Row, site.Location.Column);

                        PlugIn.Litter[site].InitialMass = mapValue;
                        PlugIn.Litter[site].Mass = mapValue;
                    }
                }
            }
        }

        private static IInputRaster<DoublePixel> MakeDoubleMap(string path)
        {
            PlugIn.ModelCore.UI.WriteLine("  Read in data from {0}", path);

            IInputRaster<DoublePixel> map;

            try
            {
                map = PlugIn.ModelCore.OpenRaster<DoublePixel>(path);
            }
            catch (FileNotFoundException)
            {
                string mesg = string.Format("Error: The file {0} does not exist", path);
                throw new System.ApplicationException(mesg);
            }

            if (map.Dimensions != PlugIn.ModelCore.Landscape.Dimensions)
            {
                string mesg = string.Format("Error: The input map {0} does not have the same dimension (row, column) as the scenario ecoregions map", path);
                throw new System.ApplicationException(mesg);
            }

            return map;
        }
    }
}