
using Landis.Core;
using System.Collections.Generic;
using System.Linq;
using Landis.SpatialModeling;
using System;

namespace Landis.Extension.Succession.BiomassPnET 
{
    /// <summary>
    /// The information for a tree species (its index and parameters).
    /// </summary>
    public class EcoregionPnET : IEcoregionPnET
    {
        #region private variables
        private Landis.Core.IEcoregion ecoregion;
        private float _precintconst;
        private float _preclossfrac;
        private float _rootingdepth;
        private string _soiltype;
        private float _leakagefrac;
        private float _fieldcap;
        private float _wiltpnt;
        private float _porosity;
        IEcoregionPnETVariables _variables;
        #endregion

        #region private static variables
        private static bool wythers;
        private static bool dtemp;
        private static Dictionary<IEcoregionPnET, Dictionary<DateTime, IEcoregionPnETVariables>> all_values = new Dictionary<IEcoregionPnET, Dictionary<DateTime, IEcoregionPnETVariables>>();
        private static Dictionary<IEcoregion, IEcoregionPnET> AllEcoregions;
        private static Landis.Library.Parameters.Ecoregions.AuxParm<string> soiltype;
        private static Landis.Library.Parameters.Ecoregions.AuxParm<float> rootingdepth;
        private static Landis.Library.Parameters.Ecoregions.AuxParm<float> precintconst;
        private static Landis.Library.Parameters.Ecoregions.AuxParm<float> preclossfrac;
        private static Landis.Library.Parameters.Ecoregions.AuxParm<float> leakagefrac;
        private static Landis.Library.Parameters.Ecoregions.AuxParm<string> climateFileName;
        #endregion

        #region accessors for private static variables
        public static List<IEcoregionPnET> Ecoregions
        {
            get 
            {
                return AllEcoregions.Values.ToList();
            }
        }
        public static IEcoregionPnET GetPnETEcoregion(IEcoregion ecoregion)
        {
            return AllEcoregions[ecoregion];
        }
        #endregion

        #region accessors for private variables

        public IEcoregionPnETVariables Variables
        { 
            get
            {
                return _variables;
            }
            set
            {
                _variables = value;
            }
        
        }

        public float FieldCap
        {
            get
            {
                return _fieldcap;
            }
            set {
                _fieldcap = value;
            }
        }
        public float WiltPnt
        {
            get
            {
                return _wiltpnt;
            }
            set {
                _wiltpnt = value;
            }
        }
        public float Porosity
        {
            get
            {
                return _porosity;
            }
            set 
            {
                _porosity = value;
            }
        }
         
        public float LeakageFrac
        {
            get {
                return _leakagefrac;
            }
        }
        public float PrecIntConst
        {
            get
            {
                return _precintconst;
            }
        }
        public float RootingDepth {
            get {
                return _rootingdepth;
            }
        }
        
        public string SoilType
        {
            get
            {
                return _soiltype;
            }
        }
        public float PrecLossFrac
        {
            get
            {
                return _preclossfrac;
            }
        }
        
        public string Description
        {
            get
            {
                return ecoregion.Description;
            }
        }
        public bool Active
        {
            get
            {
                return ecoregion.Active;
            }
        }
        public ushort MapCode
        {
            get
            {
                return ecoregion.MapCode;
            }
        }
        public int Index
        {
            get
            {
                return ecoregion.Index;
            }
        }
        public string Name
        {
            get
            {
                return ecoregion.Name;
            }
        }
        #endregion

        public static List<string> ParameterNames
        {
            get
            {
                System.Type type = typeof(EcoregionPnET); // Get type pointer
                List<string> names = type.GetProperties().Select(x => x.Name).ToList(); // Obtain all fields
                names.Add("ClimateFileName");

                return names;
            }
        }

        public static List<IEcoregionPnETVariables> GetData(IEcoregionPnET ecoregion, DateTime start, DateTime end)
        {
            // Monthly simulation data untill but not including end
            List<IEcoregionPnETVariables> data = new List<IEcoregionPnETVariables>();

            // Date: the last date in the collection of running data
            DateTime date = new DateTime(start.Ticks);

             
            while (end.Ticks > date.Ticks)
            {
                if (all_values[ecoregion].ContainsKey(date) == false)
                {
                    IObservedClimate observedClimate =  ObservedClimate.GetData(ecoregion, date);

                    List<ISpeciesPNET> species = PlugIn.SpeciesPnET.AllSpecies.ToList();

                    IEcoregionPnETVariables ecoregion_variables = new EcoregionPnETVariables(observedClimate, date, wythers, dtemp, species);

                    all_values[ecoregion].Add(date, ecoregion_variables);

                }
                data.Add(all_values[ecoregion][date]);
 
                date = date.AddMonths(1);
            }
            return data;
        }

        public static void Initialize()
        {
            soiltype = (Landis.Library.Parameters.Ecoregions.AuxParm<string>)(Parameter<string>)PlugIn.GetParameter("SoilType");
            climateFileName = (Landis.Library.Parameters.Ecoregions.AuxParm<string>)(Parameter<string>)PlugIn.GetParameter("ClimateFileName");
            rootingdepth = (Landis.Library.Parameters.Ecoregions.AuxParm<float>)(Parameter<float>)PlugIn.GetParameter("RootingDepth", 0, 1000);
            precintconst = (Landis.Library.Parameters.Ecoregions.AuxParm<float>)(Parameter<float>)PlugIn.GetParameter("PrecIntConst", 0, 1);
            preclossfrac = (Landis.Library.Parameters.Ecoregions.AuxParm<float>)(Parameter<float>)PlugIn.GetParameter("PrecLossFrac", 0, 1);

            wythers = ((Parameter<bool>)PlugIn.GetParameter("Wythers")).Value;
            dtemp = ((Parameter<bool>)PlugIn.GetParameter("DTemp")).Value;


            leakagefrac = (Landis.Library.Parameters.Ecoregions.AuxParm<float>)(Parameter<float>)PlugIn.GetParameter("LeakageFrac", 0, 1);
            AllEcoregions = new Dictionary<IEcoregion, IEcoregionPnET>();
            foreach (IEcoregion ecoregion in PlugIn.ModelCore.Ecoregions)
            {
                AllEcoregions.Add(ecoregion, new EcoregionPnET(ecoregion));
            }

            all_values = new Dictionary<IEcoregionPnET, Dictionary<DateTime, IEcoregionPnETVariables>>();
            foreach (IEcoregionPnET ecoregion in EcoregionPnET.AllEcoregions.Values)
            {
                all_values[ecoregion] = new Dictionary<DateTime, IEcoregionPnETVariables>();
            }
             
            
        }

        public EcoregionPnET(Landis.Core.IEcoregion ecoregion)
        {
            

            this.ecoregion = ecoregion;
            this._rootingdepth = rootingdepth[ecoregion];
            this._soiltype = soiltype[ecoregion];
            this._precintconst = precintconst[ecoregion];
            this._preclossfrac = preclossfrac[ecoregion];
            this._leakagefrac = leakagefrac[ecoregion];
          
        }
    }
}
 