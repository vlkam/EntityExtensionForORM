
using SQLite.Net;
using SQLite.Net.Attributes;
using System;
using System.Text;

namespace EntityExtensionForORM
{
    public class UUID : ISerializable<byte[]>
    {
        public byte[] id {
            get;
            set; }

        [Ignore]
        public Guid guid => new Guid(id);

        [Ignore]
        public string Hex { get
            {
                StringBuilder hex = new StringBuilder(id.Length * 2);
                foreach (byte b in id)  hex.AppendFormat("{0:x2}", b);
                return "X'"+hex+"'";
            }
        }

        public override string ToString() => id == null ? Guid.Empty.ToString() : new Guid(id).ToString();
        public static bool operator != (UUID u1,UUID u2) => !Equals(u1,u2);
        public static bool operator == (UUID u1,UUID u2) => Equals(u1,u2);
        public byte[] Serialize() => id;
        
        public UUID(){
            id = Guid.NewGuid().ToByteArray();
        }

        public UUID(byte[] id_){
            id = id_;
        }

        public UUID(Guid id_) {
            id = id_.ToByteArray();
        }

        public UUID(string id_)
        {
            Guid t;
            if (!Guid.TryParse(id_,out t)) throw new FormatException("Inavlid UUID string in constructor");
            id = t.ToByteArray();
        }

        public override bool Equals(object obj)
        {
            if (!(obj is UUID)) return false;
            for (byte i = 0; i < 16; i++) if (id[i] != ((UUID)obj).id[i]) return false;
            return true;
        }

        public override int GetHashCode() =>  id[0] ^ (id[1] << 16) | id[2] ^ (id[3] << 24) | id[4];

    }
}
