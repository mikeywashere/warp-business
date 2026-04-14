namespace WarpBusiness.Cli.Data;

public static class WordData
{
    public static readonly string[] Adjectives =
    [
        "rapid", "azure", "bold", "crisp", "dynamic", "eager", "fierce", "golden", "humble", "iron",
        "keen", "lively", "merry", "noble", "open", "proud", "quiet", "resilient", "silver", "swift",
        "tender", "unique", "vibrant", "warm", "young", "zesty", "amber", "bright", "calm", "daring",
        "epic", "fresh", "grand", "hardy", "iconic", "just", "kinetic", "loyal", "modern", "natural",
        "optimal", "prime", "quick", "robust", "solid", "true", "ultra", "vast", "wise", "xenial",
        "youthful", "zealous", "active", "balanced", "clear", "devoted", "elegant", "fluid", "global",
        "honest", "ideal", "jolly", "known", "light", "mindful", "nimble", "orderly", "peaceful",
        "refined", "stable", "trusted", "unified", "valid", "worthy", "exact", "fair", "graceful",
        "helpful", "infinite", "joyful", "kind", "leading", "massive", "neat", "operative", "peak",
        "rational", "strategic", "thorough", "ultimate", "vital", "stellar", "radiant", "magnetic",
        "forward", "driven", "central", "agile", "bright", "capable", "decisive", "enduring", "focused",
        "genuine", "hopeful", "integral", "lasting", "metric", "next", "proven", "quality", "reliable",
        "sincere", "timely", "upbeat", "vested", "winning", "xact", "yearning", "zonal"
    ];

    public static readonly string[] Nouns =
    [
        "atlas", "bridge", "crest", "dawn", "edge", "forge", "grove", "harbor", "isle", "junction",
        "kite", "ledge", "mast", "nexus", "orbit", "peak", "quill", "ridge", "summit", "tide",
        "union", "vale", "wave", "yield", "zenith", "anchor", "basin", "cliff", "delta", "ember",
        "field", "gate", "helm", "inlet", "jetty", "knoll", "lagoon", "mesa", "nook", "outpost",
        "plain", "quay", "reef", "shore", "trail", "upstream", "vista", "wharf", "axis", "bay",
        "cape", "dome", "estuary", "fjord", "glacier", "haven", "islet", "keystone", "landmark",
        "manor", "narrows", "oasis", "passage", "quarters", "range", "station", "terrace", "upstream",
        "vault", "watershed", "yard", "zone", "beacon", "channel", "dune", "escarpment", "ford",
        "grange", "highland", "ironworks", "keystone", "lowland", "mill", "notch", "overlook",
        "platform", "quarry", "riverbank", "silo", "tower", "undercroft", "verge", "wellspring",
        "crossroads", "depot", "frontier", "gateway", "headland", "impasse", "junction", "landmark"
    ];

    public static readonly string[] CompanySuffixes =
    [
        "Inc.", "LLC", "Corp.", "Group", "Solutions", "Services", "Systems", "Technologies",
        "Partners", "Ventures", "Dynamics", "Holdings", "Enterprises", "Labs", "Works"
    ];

    public static readonly string[] Industries =
    [
        "Technology", "Healthcare", "Finance", "Manufacturing", "Retail", "Education",
        "Hospitality", "Transportation", "Real Estate", "Media", "Energy", "Agriculture",
        "Legal Services", "Consulting", "Insurance", "Telecommunications", "Aerospace",
        "Food & Beverage", "Construction", "Pharmaceuticals"
    ];

    public static readonly string[] Departments =
    [
        "Engineering", "Sales", "Marketing", "Finance", "Human Resources", "Operations",
        "Customer Success", "Product", "Legal", "IT", "Research & Development",
        "Procurement", "Quality Assurance", "Business Development"
    ];

    public static readonly Dictionary<string, string[]> JobTitlesByDepartment = new()
    {
        ["Engineering"] = ["Software Engineer", "Senior Engineer", "Staff Engineer", "Engineering Manager", "QA Engineer", "DevOps Engineer"],
        ["Sales"] = ["Account Executive", "Sales Representative", "Senior Account Executive", "Sales Manager", "Business Development Representative", "Sales Director"],
        ["Marketing"] = ["Marketing Specialist", "Content Manager", "Marketing Manager", "Brand Strategist", "Digital Marketing Analyst", "Marketing Director"],
        ["Finance"] = ["Financial Analyst", "Accountant", "Senior Accountant", "Finance Manager", "Controller", "CFO"],
        ["Human Resources"] = ["HR Coordinator", "HR Generalist", "Recruiter", "HR Manager", "Talent Acquisition Specialist", "HR Director"],
        ["Operations"] = ["Operations Analyst", "Operations Manager", "Process Improvement Specialist", "Operations Director", "Chief Operating Officer"],
        ["Customer Success"] = ["Customer Success Manager", "Support Specialist", "Customer Success Representative", "Account Manager", "Customer Success Director"],
        ["Product"] = ["Product Manager", "Senior Product Manager", "Product Analyst", "Principal Product Manager", "VP of Product"],
        ["Legal"] = ["Paralegal", "Legal Counsel", "Associate Attorney", "Senior Counsel", "General Counsel"],
        ["IT"] = ["IT Support Specialist", "Systems Administrator", "Network Engineer", "IT Manager", "Security Engineer", "IT Director"],
        ["Research & Development"] = ["Research Scientist", "R&D Engineer", "Senior Researcher", "Principal Scientist", "R&D Manager"],
        ["Procurement"] = ["Procurement Specialist", "Buyer", "Procurement Manager", "Supply Chain Analyst", "Director of Procurement"],
        ["Quality Assurance"] = ["QA Analyst", "QA Engineer", "Senior QA Engineer", "QA Manager", "Quality Director"],
        ["Business Development"] = ["Business Development Manager", "Partnership Manager", "Strategic Alliances Manager", "VP Business Development"]
    };

    public static readonly string[] StreetNames =
    [
        "Main", "Oak", "Maple", "Cedar", "Pine", "Elm", "Washington", "Park", "Lake", "River",
        "Hill", "Forest", "Valley", "Spring", "Meadow", "Highland", "Sunset", "Sunrise", "Cherry",
        "Walnut", "Lincoln", "Jefferson", "Madison", "Adams", "Franklin", "Grant", "Sherman",
        "Sheridan", "Pershing", "Harrison", "Monroe", "Jackson", "Willow", "Birch", "Aspen",
        "Poplar", "Sycamore", "Magnolia", "Spruce", "Hickory"
    ];

    public static readonly string[] StreetTypes =
    [
        "Street", "Avenue", "Boulevard", "Drive", "Lane", "Court", "Circle", "Way", "Place", "Road",
        "Terrace", "Trail"
    ];
}
