using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace HDD_Index.Models;

/// <summary>
/// 磁盘文件的节点数据结构
/// </summary>
public class FileNode : TreeNodeBase
{
    /// <summary>
    /// 设计上允许一个磁盘文件声明持有多个Repository中的节点，
    /// 从而方便在Repository中整理分类
    /// </summary>
    public List<DeclareRepoNodeData> DeclareRepoNodeDatas { get; set; } =
        new List<DeclareRepoNodeData>();
    
    public static FileNode? CreateByJson(string json)
    {
        var root = JsonSerializer.Deserialize<FileNode>(json);
        root?.SetParent();
        return root;
    }

    public static FileNode? CreateByPath(string path)
    {
        try
        {
            var fileName = Path.GetFileName(path);
            var fileNode = new FileNode()
            {
                Name = fileName,
                IsDirectory = true,
            };
 
            // 遍历文件
            foreach (var file in Directory.GetFiles(path))
            {
                var childFileName = Path.GetFileName(file);
                var childFileNode = new FileNode()
                {
                    Name = childFileName,
                    IsDirectory = false,
                };
                fileNode.Children.Add(childFileNode);
            }

            // 递归遍历子目录
            foreach (var dir in Directory.GetDirectories(path))
            {
                var child = CreateByPath(dir);
                fileNode.Children.Add(child);
            }
            
            return fileNode;
        }
        catch (UnauthorizedAccessException)
        {
            Console.WriteLine($"无权限访问: {path}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"遍历出错: {ex.Message}");
        }
        
        return null;
    }
}

/// <summary>
/// 存储这个磁盘文件节点对应声明持有了哪个Repository中的节点
/// </summary>
public class DeclareRepoNodeData : ICloneable
{
    public string RepoNodePath { get; set; }
    
    public object Clone()
    {
        return this.MemberwiseClone();
    }
}