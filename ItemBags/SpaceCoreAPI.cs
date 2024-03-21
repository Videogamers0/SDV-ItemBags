using StardewValley;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ItemBags
{
    /// <summary>Taken from: https://github.com/spacechase0/SpaceCore_SDV/blob/master/Api.cs</summary>
    public interface ISpaceCoreAPI
    {
        string[] GetCustomSkills();
        int GetLevelForCustomSkill(Farmer farmer, string skill);
        int GetExperienceForCustomSkill(Farmer farmer, string skill);
        List<Tuple<string, int, int>> GetExperienceAndLevelsForCustomSkill(Farmer farmer);
        void AddExperienceForCustomSkill(Farmer farmer, string skill, int amt);
        int GetProfessionId(string skill, string profession);

        /// Must have [XmlType("Mods_SOMETHINGHERE")] attribute (required to start with "Mods_")
        void RegisterSerializerType(Type type);

        void RegisterCustomProperty(Type declaringType, string name, Type propType, MethodInfo getter, MethodInfo setter);

        public event EventHandler<Action<string, Action>> AdvancedInteractionStarted;
    }
}
