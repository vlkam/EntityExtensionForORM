
using SQLite.Net;
using System;

namespace EntityExtensionForORM
{
    public class UUID : ISerializable<byte[]>
    {
        byte[] id { get; set; }

        public Guid guid { get { return new Guid(id); } }

        public override string ToString() => id == null ? Guid.Empty.ToString() : new Guid(id).ToString();
        public static bool operator != (UUID u1,UUID u2) => !Equals(u1,u2);
        public static bool operator == (UUID u1,UUID u2) => Equals(u1,u2);
        public byte[] Serialize() => id;
        
        public UUID(){id = Guid.NewGuid().ToByteArray();}
        public UUID(byte[] id_){id = id_;}
        public UUID(Guid id_) { id = id_.ToByteArray(); }

        public override bool Equals(object obj)
        {
            if (obj == null) return false;
            for (byte i = 0; i < 16; i++) if (this.id[i] != ((UUID)obj).id[i]) return false;
            return true;
        }

        public override int GetHashCode()
        {
            int hash = 0;
            for (byte i = 0; i < 16; i++) hash += (this.id[i]<<i);
            return hash;
        }
    }
}
