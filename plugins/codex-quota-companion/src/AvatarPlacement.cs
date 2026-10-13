using System;
using System.IO;
using System.Collections.Generic;
namespace CodexQuotaWidget
{
    internal sealed class AvatarPlacement
    {
        internal double X,Y;
        private static readonly string FilePath=Path.Combine(CompanionPaths.DataDirectory,"角色位置.json");
        internal static AvatarPlacement Load()
        {
            try
            {
                if(!File.Exists(FilePath))return null;
                var item=Data.Json.DeserializeObject(File.ReadAllText(FilePath)) as IDictionary<string,object>;
                // Version 1 stored absolute offsets and can put the avatar outside
                // a smaller client. Start those installations at the docked corner.
                if(!Object.Equals(Data.Get(item,"version"),2))return null;
                double? x=Data.Number(Data.Get(item,"x")),y=Data.Number(Data.Get(item,"y"));
                if(!x.HasValue||!y.HasValue||x.Value<0||x.Value>1||y.Value<0||y.Value>1)return null;
                return new AvatarPlacement{X=x.Value,Y=y.Value};
            }
            catch(IOException){return null;}catch(UnauthorizedAccessException){return null;}catch(ArgumentException){return null;}catch(InvalidOperationException){return null;}
        }
        internal void Save()
        {
            try{File.WriteAllText(FilePath,Data.Json.Serialize(new{version=2,x=X,y=Y}));}
            catch(IOException){}catch(UnauthorizedAccessException){}
        }
    }
}
