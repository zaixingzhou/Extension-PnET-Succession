// uses dominance to allocate psn and subtract transpiration from soil water, average cohort vars over layer

using Landis.SpatialModeling;
using Landis.Core;
using Edu.Wisc.Forest.Flel.Util;
using System;
using System.Collections.Generic;

namespace Landis.Extension.Succession.BiomassPnET 
{
    public class Cohort : Landis.Library.AgeOnlyCohorts.ICohort, Landis.Library.BiomassCohorts.ICohort, IDisposable
    {
        public static event Landis.Library.BiomassCohorts.DeathEventHandler<Landis.Library.BiomassCohorts.DeathEventArgs> DeathEvent;
        public static event Landis.Library.BiomassCohorts.DeathEventHandler<Landis.Library.BiomassCohorts.DeathEventArgs> AgeOnlyDeathEvent;

        public byte Layer;

        public delegate void SubtractTranspiration(float transpiration, ISpeciesPNET Species);
        public delegate void AddWoodyDebris(float Litter, float KWdLit);
        public delegate void AddLitter(float AddLitter, ISpeciesPNET Species);

        public static SubtractTranspiration subtract_transpiration;
        public static AddWoodyDebris addwoodydebris;
        public static AddLitter addlitter;
        
        
        private float biomassmax;
        private float biomass; // root + wood
        private float fol;
        private float nsc;
        private ushort age;
        
        public float fage;
        public ushort index;
        
        private ISpeciesPNET species;
        private LocalOutput cohortoutput;
        private SubCohortVars canopy;
        private SubCohortVars layer;
       
        public void Dispose()
        {
            if (canopy != null) canopy.Add(layer);
        }

        public ushort Age
        {
            get
            {
                return age;
            }
        }
        public float NSC
        {
            get
            {
                return nsc;
            }
        }
        public float Fol
        {
            get
            {
                return fol;
            }
        }
        public int Biomass
        {
            get
            {
                return (int)biomass;
            }
        }
        public uint Wood
        {
            get
            {
                return (uint)((1 - species.FracBelowG) * biomass);
            }
        }
        public uint Root
        {
            get
            {
                return (uint)(species.FracBelowG * biomass);
            }
        }
        public void ReduceBiomass(double fraction)
        {
            biomass *= (float)(1.0 - fraction);
            fol *= (float)(1.0 - fraction);
        }
        public float BiomassMax
        {
            get
            {
                return biomassmax;
            }
        }
        public SubCohortVars Canopy
        {
            get
            {
                return canopy;
            }
        }
 
        

        public void Accumulate(Cohort c)
        {
            biomass += c.biomass;
            biomassmax = Math.Max(biomassmax, biomass);
            fol += c.Fol;
        }
       
        public string outputfilename
        {
            get
            {
                return cohortoutput.FileName;
            }
        }
        public float NSCfrac
        {
            get
            {
                return nsc / (FActiveBiom * (biomass + fol));
            }
        }
        public ISpeciesPNET SpeciesPNET
        {
            get
            {
                return species;
            }
        }
        public Landis.Core.ISpecies Species
        {
            get
            {
                return species;
            }
        }

        public Cohort(ISpeciesPNET species, ushort year_of_birth, string SiteName)
        {
            this.species = species;
            age = 0; 
           
            this.nsc = (ushort)species.InitialNSC;

           
            this.biomass = (uint)(1F / species.DNSC * (ushort)species.InitialNSC);
            biomassmax = biomass;

            // First declare default variables
            
            // Then overwrite them if you need stuff for outputs6+
            if (SiteName != null)
            {
                InitializeOutput(SiteName, year_of_birth, PlugIn.ModelCore.UI.WriteLine);
            }
             
        }
        public Cohort(Cohort cohort)
        {
            this.species = cohort.species;
            this.age = cohort.age;
            this.nsc = cohort.nsc;
            this.biomass = cohort.biomass;
            biomassmax = cohort.biomassmax;
            this.fol = cohort.fol;
          

        }
        public static void SetSiteAccessFunctions(SiteCohorts sitecohorts)
        {
             Cohort.subtract_transpiration = sitecohorts.SubtractTranspiration;
             Cohort.addlitter = sitecohorts.AddLitter;
             Cohort.addwoodydebris = sitecohorts.AddWoodyDebris;
             Cohort.ecoregion = sitecohorts.Ecoregion;
        }
        bool leaf_on = true;

        public static IEcoregionPnET ecoregion;

        public static bool LeafOn(float Tmin, float PsnTMin)
        {
            bool Leaf_on = Tmin > PsnTMin;
            return Leaf_on;
        }

        public SubCohortVars CalculatePhotosynthesis(EcoregionPnETVariables monthdata, float one_over_nr_of_cohorts, float LeakagePerCohort, ref float Water, ref uint PressureHead, ref float SubCanopyPar, ref float CanopyLAI)
        {
            layer = new SubCohortVars();
           
            layer.LAI = PlugIn.fIMAX * fol / (species.SLWmax - species.SLWDel * index * PlugIn.fIMAX * fol);
            CanopyLAI += layer.LAI;

            layer.Interception = monthdata.Precin * (float)(1 - Math.Exp(-1 * ecoregion.PrecIntConst * layer.LAI));

            Hydrology.WaterIn = monthdata.PrecIntffective * one_over_nr_of_cohorts - layer.Interception + monthdata.SnowMelt;//mm  \

            Water +=  Hydrology.WaterIn ;

            // Leakage 
            Hydrology.Leakage = Math.Max(LeakagePerCohort * (Water - ecoregion.FieldCap), 0);
            Water -= (ushort)Hydrology.Leakage;

            // Instantaneous runoff (excess of porosity)
            Hydrology.RunOff = Math.Max(Water - ecoregion.Porosity, 0);
            Water -= (ushort)Hydrology.RunOff;

            PressureHead = (ushort)Hydrology.Pressureheadtable[(IEcoregion)ecoregion, (ushort)Water];
 
            if (index == PlugIn.IMAX - 1)
            {
                index = 0;

                layer.MaintenanceRespiration = (float)Math.Min(NSC, monthdata[Species.Name].MaintRespFTempResp * biomass);//gC //IMAXinverse
                nsc -= layer.MaintenanceRespiration;

                if (monthdata.Month == (int)Constants.Months.January)
                {

                    addwoodydebris(Senescence(), species.KWdLit);

                    float Allocation = Math.Max(nsc - (species.DNSC * FActiveBiom * biomass), 0);
                    biomass += Allocation;
                    biomassmax = Math.Max(biomassmax, biomass);
                    nsc -= Allocation;

                    age++;
                    
                }

                bool Leaf_on = LeafOn( monthdata.Tmin, SpeciesPNET.PsnTMin);

                if (leaf_on == true && Leaf_on  == false)
                {
                    leaf_on = false;
                    addlitter(FoliageSenescence(), SpeciesPNET);
                }
                leaf_on = Leaf_on;
            }
            else index++;

            if (leaf_on == false) return layer;

            float IdealFol = (species.FracFol * FActiveBiom * biomass);
            
            if (IdealFol > fol)
            {
                float Folalloc = Math.Max(0, Math.Min(nsc, species.CFracBiomass * (IdealFol - fol))); // gC/mo

                fol += Folalloc / species.CFracBiomass;// gDW
                nsc -= Folalloc;
            }
            
            if (Fol == 0) return layer;
           
            layer.FRad = CumputeFrad(SubCanopyPar, species.HalfSat);

            SubCanopyPar *= (float)Math.Exp(-species.K * layer.LAI);

            layer.FWater = CumputeFWater(species.H2, species.H3, species.H4, PressureHead);

            if (layer.FWater == 0) return layer;

            fage = Math.Max(0, 1 - (float)Math.Pow((age / (float)species.Longevity), species.PsnAgeRed));
            
            // g/mo
            layer.NetPsn = layer.FWater * layer.FRad * fage * monthdata[species.Name].FTempPSNRefNetPsn * fol;

            layer.ConductanceCO2 = (monthdata.GsInt + (monthdata.GsSlope * layer.NetPsn * Constants.MillionOverTwelve));

            layer.FolResp = layer.FWater * monthdata[species.Name].FTempRespDayRefResp * fol;

            layer.GrossPsn = layer.NetPsn + layer.FolResp;

            layer.Transpiration = layer.GrossPsn * Constants.MCO2_MC / monthdata[Species.Name].WUE_CO2_corr;

            subtract_transpiration(layer.Transpiration, SpeciesPNET);

            nsc += layer.NetPsn;

            return layer;
        }
       
        public static float CumputeFrad(float Radiation, float HalfSat)
        {
            return Radiation / (Radiation + HalfSat);
        }
        public static float CumputeFWater(float H2, float H3, float H4, float pressurehead)
        {
            if (pressurehead < 0 || pressurehead > H4) return 0;
            else if (pressurehead > H3) return 1 - ((pressurehead - H3) / (H4 - H3));
            else if (pressurehead < H2) return pressurehead / H2;
            else return 1;
        }
        public int ComputeNonWoodyBiomass(ActiveSite site)
        {
            return (int)(fol);
        }
        public static Percentage ComputeNonWoodyPercentage(Cohort cohort, ActiveSite site)
        {
            return new Percentage(cohort.fol / (cohort.Wood + cohort.Fol));
        }
        public void InitializeOutput(string SiteName, ushort YearOfBirth, LocalOutput.SendMsg SendMsg)
        {
            cohortoutput = new LocalOutput(SiteName, "Cohort_" + Species.Name + "_" + YearOfBirth + ".csv", OutputHeader, SendMsg);
            canopy = new SubCohortVars();
        }
        
        public void UpdateCohortData(EcoregionPnETVariables monthdata )
        {



            string s = Math.Round(monthdata.Year, 2) + "," + 
                        Age + "," +
                        Layer + "," + 
                       //canopy.ConductanceCO2 + "," +
                       canopy.LAI + "," +
                       canopy.GrossPsn + "," +
                       canopy.FolResp + "," +
                       canopy.MaintenanceRespiration + "," +
                       canopy.NetPsn + "," +                  // Sum over canopy layers
                       canopy.Transpiration + "," +
                       ((canopy.FolResp > 0) ? canopy.GrossPsn / canopy.FolResp : 0).ToString() + "," +
                       fol + "," + 
                       Root + "," + 
                       Wood + "," + 
                       NSC + "," +
                       NSCfrac + "," +
                       canopy.FWater + "," +
                       canopy.FRad + "," +
                       monthdata[Species.Name].FTempPSN + "," +
                       monthdata[Species.Name].FTempResp + "," +
                       fage + "," +
                       Cohort.LeafOn(monthdata.Tmin,SpeciesPNET.PsnTMin) + "," +
                       FActiveBiom;
             
            cohortoutput.Add(s);

            canopy.Reset();
        }

        public string OutputHeader
        {
            get
            {
                string hdr = OutputHeaders.Time + "," + 
                            OutputHeaders.Age + "," +
                            //OutputHeaders.ConductanceCO2 + "," + 
                            OutputHeaders.Layer + "," + 
                            OutputHeaders.LAI + "," +
                            OutputHeaders.GrossPsn + "," + 
                            OutputHeaders.FolResp + "," + 
                            OutputHeaders.MaintResp + "," + 
                            OutputHeaders.NetPsn + "," +
                            OutputHeaders.Transpiration + "," +
                            OutputHeaders.WUE + "," +
                            OutputHeaders.Fol + "," + 
                            OutputHeaders.Root + "," + 
                            OutputHeaders.Wood + "," +
                            OutputHeaders.NSC + "," + 
                            OutputHeaders.NSCfrac + "," + 
                            OutputHeaders.fWater + "," +  
                            OutputHeaders.fRad + "," + 
                            OutputHeaders.fTemp_psn + "," +
                            OutputHeaders.fTemp_resp + "," + 
                            OutputHeaders.fage + "," + 
                            OutputHeaders.LeafOn + "," + 
                            OutputHeaders.FActiveBiom + ",";

                return hdr;
            }
        }
        public void WriteCohortData()
        {
            cohortoutput.Write();
            canopy.Reset();
        }
         
        public float FActiveBiom
        {
            get
            {
                return (float)Math.Exp(-species.FrActWd * biomass);
            }
        }
        public bool IsAlive
        {
            get
            {
                return NSCfrac > 0.01F;
            }
        }
        public float FoliageSenescence()
        {
            // If it is fall 
            float Litter = species.TOfol * fol;
            fol -= Litter;

            return Litter;

        }
        
        public float Senescence()
        {
            float senescence = ((Root * species.TOroot) + Wood * species.TOwood);
            biomass -= senescence;

            return senescence;
        }
         
        //---------------------------------------------------------------------
        /// <summary>
        /// Raises a Cohort.AgeOnlyDeathEvent.
        /// </summary>
        public static void RaiseDeathEvent(object sender,
                                Cohort cohort, 
                                ActiveSite site,
                                ExtensionType disturbanceType)
        {
            if (AgeOnlyDeathEvent != null)
            {
                AgeOnlyDeathEvent(sender, new Landis.Library.BiomassCohorts.DeathEventArgs(cohort, site, disturbanceType));
            }
            if (DeathEvent != null)
            {
                DeathEvent(sender, new Landis.Library.BiomassCohorts.DeathEventArgs(cohort, site, disturbanceType));
            }
           
        }
 
        
    } 
}
