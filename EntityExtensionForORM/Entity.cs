
using System;

namespace EntityExtensionForORM
{
   public class Entity {

        public enum EntityState {Undefined = 0,Added,Deleted,Detached,Modified,Unchanged}
        
        public Base Obj;
        public Type Type;
        public int Order;
        public EntityState State = EntityState.Undefined;

        public override string ToString() => Obj + "  " + Type + "  " + State;

    }
}
