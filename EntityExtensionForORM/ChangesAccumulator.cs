

using System.Collections.Generic;

namespace EntityExtensionForORM
{
    public class ChangesAccumulator
    {
        List<Entity> EntitiesList;
        
        public ChangesAccumulator()
        {
            EntitiesList = new List<Entity>();
        }

    }
}
