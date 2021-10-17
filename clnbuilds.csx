#!/usr/bin/env dotnet-script
#r "nuget: Spectre.Console, 0.41.0"

using System;
using System.Data.Common;
using System.IO;
using System.Threading;
using Microsoft.VisualBasic;
using Spectre.Console;

string[] targetNames = { "node_modules", "obj", "bin" };
var path = Args[0];
var startDirectory = new DirectoryInfo(path);
var rootDirectoryNode = Scan();

if (rootDirectoryNode.Nodes.Count == 0)
{
    AnsiConsole.MarkupLine($"\n[green]No matches found![/] {char.ConvertFromUtf32(0x1F44D)}");
    return;
}

if (!ShouldDelete())
{
    AnsiConsole.MarkupLine($"\n[green]Cancelled {char.ConvertFromUtf32(0x1F628)}[/]");
    return;
}

var leaves = rootDirectoryNode.GetLeafNodes().ToArray();
RenderDeletionProgress(leaves);

// UI functions
DirectoryNode Scan()
{
    DirectoryNode rootDirectoryNode = null;
    Tree root = null;

    AnsiConsole.Status()
        .Start("Finding build directories to clean", ctx =>
        {
            AnsiConsole.MarkupLine("[grey]Scanning recursively...[/]");
            rootDirectoryNode = BuildDirectoryTree(startDirectory, targetNames);

            if (rootDirectoryNode.Nodes.Count == 0)
                return;

            AnsiConsole.MarkupLine("[grey]Composing results...[/]");
            var formattedName = FormattedDirectoryName(rootDirectoryNode);
            root = new Tree(formattedName).Guide(TreeGuide.BoldLine);
            rootDirectoryNode = BuildDirectoryTree(startDirectory, targetNames);
            BuildTree(root, rootDirectoryNode);
        });

    if (root is not null)
    {
        AnsiConsole.Render(root);
    }

    return rootDirectoryNode;
}

void RenderDeletionProgress(DirectoryNode[] leaves)
{
    AnsiConsole.Progress()
        .Columns(
            new ProgressColumn[]
            {
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new SpinnerColumn()
            })
        .Start(ctx =>
        {
            var task = ctx.AddTask("[grey]Deleting directories[/]");
            foreach (var node in leaves)
            {
                node.Info.Delete(true);
                task.Increment(100 / leaves.Length);
            }

            task.Value = 100;
        });
}

string FormattedDirectoryName(DirectoryNode node)
{
    var coloredName = $"[blue]{node.Name}[/]";
    return node.Nodes.Count > 0
        ? coloredName
        : $"[#f08080]{char.ConvertFromUtf32(0x279C)}[/] [blue]{node.Name}[/]";
}

bool ShouldDelete()
{
    var warning = char.ConvertFromUtf32(0x1F4A5);
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine($"{warning} [yellow]Are you sure you want to remove these directories?[/] {warning}");

    AnsiConsole.Markup("[grey]Type 'yes' to continue:[/] ");
    var input = Console.ReadLine();
    return input == "yes";
}

// Logic functions
void BuildTree(Tree tree, DirectoryNode node)
{
    foreach (var directoryNode in node.Nodes)
    {
        var treeNode = tree.AddNode(FormattedDirectoryName(directoryNode));
        AddNodes(treeNode, directoryNode);
    }
}

void AddNodes(TreeNode treeNode, DirectoryNode directoryNode)
{
    foreach (var subDirectoryNode in directoryNode.Nodes)
    {
        var subTreeNode = treeNode.AddNode(FormattedDirectoryName(subDirectoryNode));
        AddNodes(subTreeNode, subDirectoryNode);
    }
}

DirectoryNode BuildDirectoryTree(DirectoryInfo startLocation, string[] matches)
{
    var node = new DirectoryNode(startLocation);

    foreach (var dir in startLocation.GetDirectories())
    {
        if (matches.Contains(dir.Name))
        {
            node.Nodes.Add(new DirectoryNode(dir));
            continue;
        }

        var subNode = BuildDirectoryTree(dir, matches);
        if (subNode.Nodes.Count > 0)
        {
            node.Nodes.Add(subNode);
        }
    }

    return node;
}

// Tree structure
public class DirectoryNode
{
    public DirectoryNode(DirectoryInfo info)
    {
        Info = info;
        Name = info.Name;
    }

    public DirectoryInfo Info { get; }
    public string Name { get; }
    public List<DirectoryNode> Nodes { get; set; } = new();

    public IEnumerable<DirectoryNode> GetLeafNodes()
    {
        foreach (var node in Nodes)
        {
            if (node.Nodes.Count == 0)
                yield return node;

            foreach (var subNode in node.GetLeafNodes())
                yield return subNode;
        }
    }
}