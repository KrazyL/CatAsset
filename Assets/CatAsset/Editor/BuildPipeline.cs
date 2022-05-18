﻿using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CatAsset.Editor
{
    /// <summary>
    /// 构建管线
    /// </summary>
    public static class BuildPipeline
    {
        /// <summary>
        /// 执行资源包构建管线
        /// </summary>
        public static void ExecuteBundleBuildPipeline(BundleBuildConfigSO bundleBuildConfig,BuildTarget targetPlatform)
        {
            List<AssetBundleBuild> assetBundleBuilds = bundleBuildConfig.GetAssetBundleBuilds();
            List<BundleBuildInfo> normalBundleBuilds = bundleBuildConfig.GetNormalBundleBuilds();
            List<BundleBuildInfo> rawBundleBuilds = bundleBuildConfig.GetRawBundleBuilds();
            AssetBundleManifest unityManifest = null;
            
 
            //创建完整资源包构建输出目录
            string fullOutputPath = GetFullOutputPath(bundleBuildConfig.OutputPath, targetPlatform,
                bundleBuildConfig.ManifestVersion);
            Util.CreateEmptyDirectory(fullOutputPath);
            
            //构建AssetBundle
            unityManifest = BuildAssetBundles(fullOutputPath, assetBundleBuilds, bundleBuildConfig.Options, targetPlatform);

            //删除所有.manifest文件
            DeleteManifestFiles(fullOutputPath);
            
            //构建原生资源包
            BuildRawBundles(fullOutputPath, rawBundleBuilds);
            
            //创建资源清单文件
            CatAssetManifest catAssetManifest = CreateManifest(fullOutputPath, bundleBuildConfig.ManifestVersion, normalBundleBuilds, rawBundleBuilds,
                unityManifest);
            
            //写入资源清单文件到构建输出目录下
            WriteManifestFile(fullOutputPath,catAssetManifest);
            
            if (bundleBuildConfig.IsCopyToReadOnlyPath && bundleBuildConfig.TargetPlatforms.Count == 1)
            {
                //复制指定资源组的资源到只读目录下
                CopyToReadOnlyPath(fullOutputPath,bundleBuildConfig.CopyGroup,catAssetManifest);
            }
        }

        /// <summary>
        /// 执行原生资源包构建管线
        /// </summary>
        public static void ExecuteRawBundleBuildPipeline(BundleBuildConfigSO bundleBuildConfig,BuildTarget targetPlatform)
        {
            //获取完整原生资源包构建输出目录
            string fullOutputPath = GetFullOutputPath(bundleBuildConfig.OutputPath, targetPlatform,
                bundleBuildConfig.ManifestVersion);
            fullOutputPath += "_rawbundles";
        }
        
        /// <summary>
        /// 获取完整资源包构建输出目录
        /// </summary>
        private static string GetFullOutputPath(string outputPath, BuildTarget targetPlatform, int manifestVersion)
        {
            string dir = Application.version + "_" + manifestVersion;
            string result = Path.Combine(outputPath, targetPlatform.ToString(), dir);
            return result;
        }

        /// <summary>
        /// 构建AssetBundle
        /// </summary>
        private static AssetBundleManifest BuildAssetBundles(string outputPath, List<AssetBundleBuild> bundleBuilds,
            BuildAssetBundleOptions options, BuildTarget targetPlatform)
        {
            if (bundleBuilds == null)
            {
                return null;
            }
            
            AssetBundleManifest unityManifest =
                UnityEditor.BuildPipeline.BuildAssetBundles(outputPath, bundleBuilds.ToArray(), options,
                    targetPlatform);
            return unityManifest;
        }

        /// <summary>
        /// 删除所有.manifest文件
        /// </summary>
        private static void DeleteManifestFiles(string outputPath)
        {
            DirectoryInfo dirInfo = new DirectoryInfo(outputPath);
            string directory = outputPath.Substring(outputPath.LastIndexOf("\\") + 1);
            foreach (FileInfo file in dirInfo.GetFiles("*", SearchOption.AllDirectories))
            {
                if (file.Name == directory || file.Extension == ".manifest")
                {
                    //删除manifest文件
                    file.Delete();
                }
            }
        }

        /// <summary>
        /// 构建原生资源包
        /// </summary>
        private static void BuildRawBundles(string outputPath, List<BundleBuildInfo> rawBundleBuilds)
        {
            foreach (BundleBuildInfo rawBundleBuildInfo in rawBundleBuilds)
            {
                string rawAssetName = rawBundleBuildInfo.Assets[0].AssetName;
                string fullDirectory = Path.Combine(outputPath, rawBundleBuildInfo.DirectoryName.ToLower());
                if (!Directory.Exists(fullDirectory))
                {
                    Directory.CreateDirectory(fullDirectory);
                }

                string targetFileName = Path.Combine(outputPath, rawBundleBuildInfo.RelativePath);
                File.Copy(rawAssetName, targetFileName); //直接将原生资源复制过去
            }
        }

        /// <summary>
        /// 生成资源清单
        /// </summary>
        private static CatAssetManifest CreateManifest(string outputPath, int manifestVersion,
            List<BundleBuildInfo> bundleBuilds, List<BundleBuildInfo> rawBundleBuilds,
            AssetBundleManifest unityManifest)
        {
            CatAssetManifest manifest = new CatAssetManifest
            {
                GameVersion = Application.version,
                ManifestVersion = manifestVersion,
            };

            //创建普通资源包的清单信息
            foreach (BundleBuildInfo bundleBuildInfo in bundleBuilds)
            {
                BundleManifestInfo bundleManifestInfo = new BundleManifestInfo()
                {
                    RelativePath = bundleBuildInfo.RelativePath,
                    Directory = bundleBuildInfo.DirectoryName,
                    BundleName = bundleBuildInfo.BundleName,
                    Group = bundleBuildInfo.Group,
                    IsRaw = false,
                };
                manifest.Bundles.Add(bundleManifestInfo);

                bundleManifestInfo.IsScene = bundleBuildInfo.Assets[0].AssetName.EndsWith(".unity");

                string fullPath = Path.Combine(outputPath, bundleBuildInfo.RelativePath);
                FileInfo fi = new FileInfo(fullPath);
                bundleManifestInfo.Length = fi.Length;

                bundleManifestInfo.Hash = unityManifest.GetAssetBundleHash(bundleBuildInfo.RelativePath);

                foreach (AssetBuildInfo assetBuildInfo in bundleBuildInfo.Assets)
                {
                    AssetManifestInfo assetManifestInfo = new AssetManifestInfo()
                    {
                        AssetName = assetBuildInfo.AssetName,
                    };
                    bundleManifestInfo.Assets.Add(assetManifestInfo);

                    //依赖列表不进行递归记录 因为加载的时候会对依赖进行递归加载
                    assetManifestInfo.Dependencies = Util.GetDependencies(assetManifestInfo.AssetName, false);
                }
            }

            //创建原生资源包的清单信息
            foreach (BundleBuildInfo bundleBuildInfo in rawBundleBuilds)
            {
                BundleManifestInfo bundleManifestInfo = new BundleManifestInfo()
                {
                    RelativePath = bundleBuildInfo.RelativePath,
                    Directory = bundleBuildInfo.DirectoryName,
                    BundleName = bundleBuildInfo.BundleName,
                    Group = bundleBuildInfo.Group,
                    IsRaw = true,
                    IsScene = false,
                };
                manifest.Bundles.Add(bundleManifestInfo);

                string fullPath = Path.Combine(outputPath, bundleBuildInfo.RelativePath);
                byte[] bytes = File.ReadAllBytes(fullPath);
                bundleManifestInfo.Length = bytes.Length;

                bundleManifestInfo.Hash = Hash128.Compute(bytes);
                
                AssetManifestInfo assetManifestInfo = new AssetManifestInfo()
                {
                    AssetName = bundleBuildInfo.Assets[0].AssetName,
                };
                bundleManifestInfo.Assets.Add(assetManifestInfo);
            }

         

            return manifest;
        }

        /// <summary>
        /// 写入资源清单文件
        /// </summary>
        private static void WriteManifestFile(string outputPath,CatAssetManifest manifest)
        {
            //写入清单文件json
            string json = CatJson.JsonParser.ToJson(manifest);
            using (StreamWriter sw = new StreamWriter(Path.Combine(outputPath, Util.ManifestFileName)))
            {
                sw.Write(json);
            }
        }

        /// <summary>
        /// 将指定资源组的资源复制到只读下
        /// </summary>
        private static void CopyToReadOnlyPath(string outputPath, string copyGroup,CatAssetManifest manifest)
        {
            //要复制的资源组的Set
            HashSet<string> copyGroupSet = null;
            if (!string.IsNullOrEmpty(copyGroup))
            {
                copyGroupSet = new HashSet<string>(copyGroup.Split(';'));
            }
            
            Util.CreateEmptyDirectory(Application.streamingAssetsPath);

            List<BundleManifestInfo> copiedBundles = new List<BundleManifestInfo>();

            //复制指定组的资源文件
            foreach (BundleManifestInfo bundleManifestInfo in manifest.Bundles)
            {
                if (copyGroupSet != null)
                {
                    if (!copyGroupSet.Contains(bundleManifestInfo.Group))
                    {
                        //跳过并非指定资源组的资源文件
                        continue;
                    }

                }

                FileInfo fi = new FileInfo(Path.Combine(outputPath, bundleManifestInfo.RelativePath));

                string fullPath = CatAsset.Util.GetReadOnlyPath(bundleManifestInfo.RelativePath);
                string fullDirectory =  CatAsset.Util.GetReadOnlyPath(bundleManifestInfo.Directory.ToLower());
                if (!Directory.Exists(fullDirectory))
                {
                    //StreamingAssets下的目录不存在则创建
                    Directory.CreateDirectory(fullDirectory);
                }
                
                fi.CopyTo(fullPath);

                copiedBundles.Add(bundleManifestInfo);
            }
            
            //根据复制过去的资源包修改资源清单
            manifest.Bundles = copiedBundles;
            
            //写入仅包含被复制的资源包的资源清单文件到只读区下
            WriteManifestFile(Application.streamingAssetsPath,manifest);

            AssetDatabase.Refresh();
        }
    }
}