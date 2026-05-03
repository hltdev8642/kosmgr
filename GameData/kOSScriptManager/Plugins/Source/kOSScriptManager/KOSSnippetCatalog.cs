using System.Collections.Generic;

namespace kOSScriptManager
{
    internal sealed class SnippetCategory
    {
        public SnippetCategory(string name, IReadOnlyList<string> snippets)
        {
            Name = name;
            Snippets = snippets;
        }

        public string Name { get; }
        public IReadOnlyList<string> Snippets { get; }
    }

    internal static class KOSSnippetCatalog
    {
        public static readonly IReadOnlyList<SnippetCategory> Categories = new List<SnippetCategory>
        {
            new SnippetCategory("Control Flow", new List<string>
            {
                "if condition {\n  // code\n}.",
                "until condition {\n  // code\n}.",
                "for part in ship:parts {\n  // code\n}.",
                "when condition then {\n  // code\n}.",
                "on condition {\n  // code\n}.",
                "function name {\n  parameter p.\n  return 0.\n}"
            }),
            new SnippetCategory("Vessel", new List<string>
            {
                "ship:name",
                "ship:velocity:surface",
                "ship:availablethrust",
                "ship:maxthrust",
                "ship:mass",
                "ship:altitude",
                "ship:status",
                "ship:facing:vector"
            }),
            new SnippetCategory("Navigation", new List<string>
            {
                "heading(90, 45)",
                "lock steering to heading(90,45).",
                "lock throttle to 1.",
                "set target to body(\"Mun\").",
                "node(time:seconds+60, 0, 100, 0)",
                "eta:seconds"
            }),
            new SnippetCategory("I/O", new List<string>
            {
                "print \"Hello\".",
                "print \"Value:\" + x.",
                "clearscreen.",
                "log \"message\" to \"flightlog.txt\".",
                "runpath(\"archive:/script.ks\").",
                "list files."
            }),
            new SnippetCategory("Math", new List<string>
            {
                "abs(x)",
                "sin(30)",
                "cos(45)",
                "tan(10)",
                "round(x, 2)",
                "sqrt(x)",
                "constant:g",
                "constant:pi"
            }),
            new SnippetCategory("Staging", new List<string>
            {
                "stage.",
                "lock throttle to 0.",
                "wait until ship:apoapsis > 70000.",
                "set nextNode to nextnode.",
                "remove nextNode."
            }),
            new SnippetCategory("Parts", new List<string>
            {
                "ship:parts",
                "ship:partstagged(\"engine\")[0]",
                "set p to ship:partsdubbed(\"LV-T45\")[0].",
                "set e to ship:partstagged(\"mainengine\")[0]:getmodule(\"ModuleEnginesFX\").",
                "set r to ship:partstagged(\"reaction\")[0]."
            }),
            new SnippetCategory("Keywords", new List<string>
            {
                "set", "lock", "unlock", "local", "global", "parameter", "declare",
                "run", "runpath", "runoncepath", "function", "return", "wait", "stage"
            })
        };
    }
}
