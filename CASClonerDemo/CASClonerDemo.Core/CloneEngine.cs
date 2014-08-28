using s4pi.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CASPartResource;
using s4pi.WrapperDealer;
using s4pi.Package;
using System.Security.Cryptography;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace CASClonerDemo.Core
{
    public static class CloneEngine
    {

        public static IPackage CloneCAS(CASPartResourceTS4 oldCASP, IPackage source, bool isReplace = false, string name = "")
        {
            IPackage result = Package.NewPackage(1);

            // Deal with CASP item right now
            CASPartResourceTS4 newCASP = oldCASP.Copy();

            Random r = new Random();
            string hashSalt = DateTime.Now.ToShortTimeString() + r.Next().ToString();
            if (name == "") name = oldCASP.Name;
            newCASP.Name = isReplace ? oldCASP.Name : name;

            // Add RLE texture files
            foreach (TGIBlock RLETGIinCASP in newCASP.TGIList.FindAll(tgi => tgi.ResourceType == 0x3453CF95))
            {
                Stream rleStream;
                IResourceKey newRLETGI = CASPCloneFromOldTGI(source, RLETGIinCASP, out rleStream);
                if (!isReplace)
                {
                    if (newRLETGI.Instance == FNV64.GetHash(oldCASP.Name))
                    {
                        newRLETGI.Instance = FNV64.GetHash(newCASP.Name) | 0x8000000000000000;
                        RLETGIinCASP.Instance = FNV64.GetHash(newCASP.Name) | 0x8000000000000000;
                        newRLETGI.ResourceGroup |= 0x80000000;
                        RLETGIinCASP.ResourceGroup |= 0x80000000;
                    }
                    else
                    {
                        // for dump map
                        newRLETGI.Instance = FNV64.GetHash(RLETGIinCASP.Instance.ToString() + hashSalt) | 0x8000000000000000;
                        RLETGIinCASP.Instance = FNV64.GetHash(RLETGIinCASP.Instance.ToString() + hashSalt) | 0x8000000000000000;
                        newRLETGI.ResourceGroup |= 0x80000000;
                        RLETGIinCASP.ResourceGroup |= 0x80000000;
                    }
                }
                if (rleStream == null) throw new InvalidOperationException("Cannot find RLE resource inside the package");
                result.AddResource(newRLETGI, rleStream, true);
            }

            
            // now for the GEOMs
            TGIBlock oldGeomReferenceList = oldCASP.TGIList.Find(tgi => tgi.ResourceType == 0xAC16FBEC);
            if (oldGeomReferenceList != null)
            {
                Stream geomResourceStream;
                IResourceKey newGeomReferenceListTGI = CASPCloneFromOldTGI(source, oldGeomReferenceList, out geomResourceStream);
                if (geomResourceStream == null) throw new InvalidOperationException("Cannot find GEOM list resource inside the package");
                GEOMListResource oldGEOMList = new GEOMListResource(1, geomResourceStream);
                GEOMListResource newGEOMList = oldGEOMList.Copy();

                ulong geomNewInstance = FNV64.GetHash(name + "geom" + hashSalt) | 0x8000000000000000;

                if (!isReplace)
                {
                    
                    foreach (var geomBlock in newGEOMList.GEOMReferenceBlockList)
                    {
                        foreach (var tgi in geomBlock.tgiList)
                        {
                            tgi.Instance = geomNewInstance;
                        }
                    }

                    newGeomReferenceListTGI.Instance = geomNewInstance;
                    newGeomReferenceListTGI.ResourceGroup |= 0x80000000;
                    newGEOMList.CurrentInstance.Instance = geomNewInstance;
                    newGEOMList.CurrentInstance.ResourceType |= 0x80000000;
                    newCASP.TGIList.Find(tgi => tgi.Instance == oldGeomReferenceList.Instance && tgi.ResourceType == oldGeomReferenceList.ResourceType).Instance = geomNewInstance;
                }

                result.AddResource(newGeomReferenceListTGI, newGEOMList.Stream, true);

                foreach (TGIBlock TGIinCASP in newCASP.TGIList.FindAll(tgi => tgi.ResourceType == 0x015A1849))
                {
                    Stream geomStream;
                    IResourceKey geomTGI = CASPCloneFromOldTGI(source, TGIinCASP, out geomStream);
                    if (!isReplace)
                    {
                        TGIinCASP.Instance = geomNewInstance;
                        geomTGI.Instance = geomNewInstance;
                        TGIinCASP.ResourceGroup |= 0x80000000;
                        geomTGI.ResourceGroup |= 0x80000000;
                    }
                    result.AddResource(geomTGI, geomStream, true);
                }
            }

            // Add RLES
            TGIBlock rlesInCASP = newCASP.TGIList.Find(tgi => tgi.ResourceType == 0xBA856C78);
            if (rlesInCASP != null)
            {
                Stream RlesStream;
                IResourceKey newRLES = CASPCloneFromOldTGI(source, rlesInCASP, out RlesStream);
                if (!isReplace)
                {
                    ulong rlesInstance = FNV64.GetHash(rlesInCASP.Instance.ToString() + hashSalt) | 0x8000000000000000;
                    rlesInCASP.Instance = rlesInstance;
                    newRLES.Instance = rlesInstance;
                    rlesInCASP.ResourceGroup |= 0x80000000;
                    newRLES.ResourceGroup = rlesInCASP.ResourceGroup;
                }
                result.AddResource(newRLES, RlesStream, true);
            }

            // Weird _IMG stiff. maybe swatch? Need to ask grant
            TGIBlock _imgInCASP = newCASP.TGIList.Find(tgi => tgi.ResourceType == 0x00B2D882);
            if(_imgInCASP != null)
            {
                Stream _imgStream;
                IResourceKey _imgNew = CASPCloneFromOldTGI(source, _imgInCASP, out _imgStream);
                if(!isReplace)
                {
                    _imgNew.Instance = FNV64.GetHash("swatch?" + hashSalt);
                    _imgNew.ResourceGroup |= 0x80000000;
                    _imgNew.Instance |= 0x8000000000000000;
                    _imgInCASP.Instance = _imgNew.Instance;
                    _imgInCASP.ResourceGroup = _imgNew.ResourceGroup;
                    _imgInCASP.Instance = _imgNew.Instance;
                }

                result.AddResource(_imgNew, _imgStream, true);
            }

            // add CASP resource finally
            TGIBlock newCASPTGI = new TGIBlock(1, null, 0x034AEECBU, 0x80000000U, FNV32.GetHash(hashSalt) | 0x8000000000000000); // normal 64 hash sometimes doesn't work
            if (!isReplace) newCASP.OutfitGroup = FNV32.GetHash(hashSalt);
            result.AddResource(newCASPTGI, newCASP.Stream, true);

            foreach(var entry in result.GetResourceList)
            {
                // compress the entry
                entry.Compressed = 0x425A;
                //entry.ResourceGroup |= 0x80000000;
                //entry.Instance |= 0x8000000000000000;
            }

            return result;
        }

        public static String GetTimestamp(this DateTime value)
        {
            return value.ToString("yyyyMMddHHmmssffff");
        }

        public static IResourceKey CASPCloneFromOldTGI(IPackage source, TGIBlock tgi, out Stream stream)
        {
            IResourceIndexEntry search = source.Find(entry => entry.ResourceType == tgi.ResourceType && entry.Instance == tgi.Instance && entry.ResourceGroup == tgi.ResourceGroup);
            if (search == null) throw new InvalidOperationException("Cannot find resource in parent package");
            IResourceKey result = search.Copy();
            stream = WrapperDealer.GetResource(source.RecommendedApiVersion, source, (IResourceIndexEntry)search).Stream;
            return result;
        }


       
    }
}
