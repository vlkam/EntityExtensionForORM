
using System;

namespace EntityExtensionForORM
{
    public class Entity {

        public enum EntityState { Undefined = 0, Added, Deleted, Detached, Modified, Unchanged }

        public WeakReference<Base> Obj;
        public Base HardReference { get; set; }
        public Type Type { get; set; }
        public int Order { get; set; }
        public EntityState State { get; set; }

        public override string ToString() => Obj + "  " + Type + "  " + State;

    }
}
