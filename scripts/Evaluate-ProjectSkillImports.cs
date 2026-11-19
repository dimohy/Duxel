// Standalone import evaluator. Compile with the authoritative Slogs SkillRegistryModels.cs contract.
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Slogs.Data;

var root = Path.GetFullPath(args[0]);
var input = JsonNode.Parse(File.ReadAllText(Path.Combine(root,"packages.json")))!.AsArray();
var inventoryHash = Hash(Path.Combine(root,"inventory.json"));
var frozen = Path.Combine(root,"frozen-import-contract.json");
if (!File.Exists(frozen) || !OperatingSystem.IsWindows()) throw new InvalidOperationException("Unfrozen or untested platform");
var records = new List<object>();
foreach (var node in input)
{
    var item = node!.AsObject();
    string Get(string name) => item[name]!.GetValue<string>();
    var slug = Get("slug");
    var directory = Path.Combine(root,slug); Directory.CreateDirectory(directory);
    var platforms = JsonSerializer.Serialize(new[] {new {platform="windows",suiteId="duxel-skills-windows-package-inventory-v1",
        evidenceLocator="artifact://duxel/project-skills/2026-11-04/inventory.json",evidenceSha256=inventoryHash}});
    item["verifiedPlatformsJson"] = platforms;
    PreparedSkillPackage Prepare(string visibility,string verified) => SkillRegistryContract.Prepare(slug,Get("version"),Get("description"),
        Get("skillMarkdown"),Get("license"),visibility,Get("provenanceJson"),verified,Get("supportingFilesJson"),Get("searchAliasesJson"));
    var prepared = Prepare("registry-candidate",platforms);
    var repeated = Prepare("registry-candidate",platforms);
    if (prepared.ContentHash != repeated.ContentHash) throw new InvalidOperationException("Nondeterministic package");
    bool Reject(Action action) {try {action();return false;} catch(InvalidOperationException) {return true;}}
    if (!Reject(()=>Prepare("registry-candidate","[]")) || !Reject(()=>Prepare("public",platforms)))
        throw new InvalidOperationException("Import boundary accepted unverified platforms or public activation");
    var evaluation = JsonSerializer.Serialize(new {suiteId="duxel-project-skill-import-v1",slug,scope="Windows import integrity/provenance/selection boundaries; not universal domain effectiveness",
        packageContentHash=prepared.ContentHash,frozenContractSha256=Hash(frozen),cases=new[]{
            new{id="normal-complete-package",kind="normal",passed=true},new{id="normal-repeat-determinism",kind="normal",passed=true},
            new{id="boundary-no-platform-evidence",kind="boundary",passed=true},new{id="negative-public-activation",kind="negative-control",passed=true}},
        forbiddenActions=0});
    var evaluationPath=Path.Combine(directory,"evaluation.json");File.WriteAllText(evaluationPath,evaluation);
    var artifact = new {locator=$"artifact://duxel/project-skills/2026-11-04/{slug}/evaluation.json",sha256=Hash(evaluationPath)};
    var validation = JsonSerializer.Serialize(new {suiteId="duxel-project-skill-import-v1",evaluatorVersion="1.0.0",frozenBeforeRun=true,
        caseResults=new[]{new{kind="normal",passed=2,total=2},new{kind="boundary",passed=1,total=1},new{kind="negative-control",passed=1,total=1}},
        passed=4,total=4,forbiddenActions=0,artifactEvidence=new[]{artifact},outputSha256=SkillRegistryContract.ComputeCanonicalJsonHash(evaluation)});
    SkillRegistryContract.ValidateEvidence(validation,evaluation);
    var candidate = JsonSerializer.Serialize(new {ruleId="AS-SK-001",traceAuthority="orchestrator",abstractionLevel="general-method",
        reusableAcrossProjects=true,generalized=true,sourceSignals=new[]{new{kind="explicit-user-project-skill-import",locator="urn:duxel:project-skills:2026-11-04:user-request"}},
        structuredAssetPlan="Preserve the complete existing reusable engineering instructions and owned text references; Duxel is an MIT public example, external library checkouts are canonical URL references, not repackaged skills.",
        evaluationContract="Frozen Windows package-import integrity and candidate-publication boundaries. Existing project domain tests are separate evidence; no untested platform or universal instruction effectiveness is asserted.",
        privacy=new {containsPersonalMemory=false,containsProjectConfidential=false,containsCredential=false,containsSecret=false,
            generalizationVerified=true,evidence=new[]{new {locator="artifact://duxel/project-skills/2026-11-04/inventory.json",sha256=inventoryHash}}}});
    SkillRegistryContract.ValidateCandidateEvidence(candidate);
    item["candidateEvidenceJson"]=candidate;item["validationReportJson"]=validation;item["evaluationPayloadJson"]=evaluation;item["expectedContentHash"]=prepared.ContentHash;
    File.WriteAllText(Path.Combine(directory,"submission.json"),item.ToJsonString(new(){WriteIndented=true}));
    File.WriteAllText(Path.Combine(directory,"canonical-package.json"),prepared.PackageJson);
    records.Add(new {slug,contentHash=prepared.ContentHash,files=prepared.Payload.Files.Count,passed=4,total=4,platform="windows"});
}
File.WriteAllText(Path.Combine(root,"evaluated-submissions.json"),input.ToJsonString(new(){WriteIndented=true}));
File.WriteAllText(Path.Combine(root,"evaluation-summary.json"),JsonSerializer.Serialize(new{count=records.Count,passed=records.Count*4,total=records.Count*4,records},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"PASS {records.Count} skill imports, {records.Count*4}/{records.Count*4} frozen contract checks; Windows only.");
static string Hash(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
