
using System;

namespace EntityExtensionForORM
{
    public class Entity {

        public enum EntityState { Undefined = 0, Added, Deleted, Detached, Modified, Unchanged }

        public WeakReference<Base> Obj;
        public Base HardReference;
        public Type Type;
        public int Order;
        public EntityState State;

        public bool IsNeedSynchronize;
        public EntityState PreviousState;

        public override string ToString() => Obj + "  " + Type + "  " + State;

    }
}
