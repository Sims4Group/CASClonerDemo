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
    public class CloneEngine
    {

        public static IPackage CloneCAS(CASPartResourceTS4 oldCASP, IPackage source, bool isReplace = false, string name = "")
        {
            IPackage result = Package.NewPackage(1);

            // Deal with CASP item right now
            CASPartResourceTS4 newCASP = oldCASP.Copy();

            Random r = new Random();
            string hashSalt = DateTime.Now.ToShortTimeString() + r.Next().ToString();
            if (name == "") name = oldCASP.Name;
            newCASP.Name = isReplace? oldCASP.Name : name;

            // Add RLE texture files
            foreach (TGIBlock RLETGI in newCASP.TGIList.FindAll(tgi => tgi.ResourceType == 0x3453CF95))
            {
                Stream rleStream;
                IResourceKey newRLETGI = CASPCloneFromOldTGI(source, RLETGI, out rleStream);
                if (!isReplace)
                {
                    if (newRLETGI.Instance == FNV64.GetHash(oldCASP.Name))
                    {
                        newRLETGI.Instance = FNV64.GetHash(name);
                        RLETGI.Instance = FNV64.GetHash(name);
                    }
                    else
                    {
                        // for dump map
                        newRLETGI.Instance = FNV64.GetHash(RLETGI.Instance.ToString() + hashSalt);
                        RLETGI.Instance = FNV64.GetHash(RLETGI.Instance.ToString() + hashSalt);
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

                ulong geomNewInstance = FNV64.GetHash(name + "geom" + hashSalt);

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
                    newGEOMList.CurrentInstance.Instance = geomNewInstance;
                    newCASP.TGIList.Find(tgi => tgi.Instance == oldGeomReferenceList.Instance && tgi.ResourceType == oldGeomReferenceList.ResourceType).Instance = geomNewInstance;
                }

                result.AddResource(newGeomReferenceListTGI, newGEOMList.Stream, true);

                foreach (TGIBlock TGI in newCASP.TGIList.FindAll(tgi => tgi.ResourceType == 0x015A1849))
                {
                    Stream geomStream;
                    IResourceKey geomTGI = CASPCloneFromOldTGI(source, TGI, out geomStream);
                    if (!isReplace)
                    {
                        TGI.Instance = geomNewInstance;
                        geomTGI.Instance = geomNewInstance;
                    }
                    result.AddResource(geomTGI, geomStream, true);
                }
            }
            // Add RLES
            TGIBlock rles = newCASP.TGIList.Find(tgi => tgi.ResourceType == 0xBA856C78);
            if (rles != null)
            {
                Stream RlesStream;
                IResourceKey newRLES = CASPCloneFromOldTGI(source, rles, out RlesStream);
                if (!isReplace)
                {
                    ulong rlesInstance = FNV64.GetHash(rles.Instance.ToString() + hashSalt);
                    rles.Instance = rlesInstance;
                    newRLES.Instance = rlesInstance;
                }
                result.AddResource(newRLES, RlesStream, true);
            }

            // Weird _IMG stiff. maybe swatch? Need to ask grant
            TGIBlock _imgOld = newCASP.TGIList.Find(tgi => tgi.ResourceType == 0x00B2D882);
            if(_imgOld != null)
            {
                Stream _imgStream;
                IResourceKey _imgNew = CASPCloneFromOldTGI(source, _imgOld, out _imgStream);
                if(!isReplace)
                {
                    _imgNew.Instance = FNV64.GetHash("swatch?" + hashSalt);
                }

                result.AddResource(_imgNew, _imgStream, true);
            }

            // add CASP resource finally
            TGIBlock newCASPTGI = new TGIBlock(1, null, 0x034AEECBU, 0U, FNV32.GetHash(hashSalt)); // normal 64 hash sometimes doesn't work
            if (!isReplace) newCASP.OutfitGroup = FNV32.GetHash(hashSalt);
            result.AddResource(newCASPTGI, newCASP.Stream, true);

            return result;
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
