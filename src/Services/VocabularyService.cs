using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ScottWisper.Models;

namespace ScottWisper.Services
{
    /// <summary>
    /// Interface for vocabulary management service
    /// </summary>
    public interface IVocabularyService
    {
        /// <summary>
        /// Get all available vocabulary packs
        /// </summary>
        Task<List<VocabularyPack>> GetAllPacksAsync();
        
        /// <summary>
        /// Get enabled vocabulary packs
        /// </summary>
        Task<List<VocabularyPack>> GetEnabledPacksAsync();
        
        /// <summary>
        /// Enable a vocabulary pack
        /// </summary>
        Task<bool> EnablePackAsync(string packId);
        
        /// <summary>
        /// Disable a vocabulary pack
        /// </summary>
        Task<bool> DisablePackAsync(string packId);
        
        /// <summary>
        /// Get a specific vocabulary pack
        /// </summary>
        Task<VocabularyPack?> GetPackAsync(string packId);
        
        /// <summary>
        /// Get the medical vocabulary pack
        /// </summary>
        Task<VocabularyPack?> GetMedicalPackAsync();
        
        /// <summary>
        /// Get the legal vocabulary pack
        /// </summary>
        Task<VocabularyPack?> GetLegalPackAsync();
        
        /// <summary>
        /// Add a custom term
        /// </summary>
        Task<VocabularyTerm> AddCustomTermAsync(string term, string? category = null, string? definition = null);
        
        /// <summary>
        /// Remove a custom term
        /// </summary>
        Task<bool> RemoveCustomTermAsync(string termId);
        
        /// <summary>
        /// Get all custom terms
        /// </summary>
        Task<List<VocabularyTerm>> GetCustomTermsAsync();
        
        /// <summary>
        /// Add a proper noun
        /// </summary>
        Task AddProperNounAsync(string name);
        
        /// <summary>
        /// Remove a proper noun
        /// </summary>
        Task<bool> RemoveProperNounAsync(string name);
        
        /// <summary>
        /// Add an acronym
        /// </summary>
        Task AddAcronymAsync(string acronym, string fullForm);
        
        /// <summary>
        /// Get context string for transcription
        /// </summary>
        Task<string> GetTranscriptionContextAsync();
        
        /// <summary>
        /// Enhance transcription with vocabulary corrections
        /// </summary>
        Task<VocabularyEnhancedResult> EnhanceTranscriptionAsync(string transcription);
        
        /// <summary>
        /// Get vocabulary statistics
        /// </summary>
        Task<VocabularyStatistics> GetStatisticsAsync();
        
        /// <summary>
        /// Import vocabulary from file
        /// </summary>
        Task<bool> ImportVocabularyAsync(string filePath);
        
        /// <summary>
        /// Export vocabulary to file
        /// </summary>
        Task<string> ExportVocabularyAsync(string? filePath = null);
        
        /// <summary>
        /// Reset to defaults
        /// </summary>
        Task ResetToDefaultsAsync();
    }
    
    /// <summary>
    /// Implementation of vocabulary service
    /// </summary>
    public class VocabularyService : IVocabularyService
    {
        private readonly ILogger<VocabularyService> _logger;
        private readonly string _dataDirectory;
        private readonly List<VocabularyPack> _packs;
        private CustomVocabulary _customVocabulary = null!;
        private readonly object _lock = new();
        
        public VocabularyService(ILogger<VocabularyService> logger, string? dataDirectory = null)
        {
            _logger = logger;
            
            // Default data directory in AppData
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _dataDirectory = dataDirectory ?? Path.Combine(appDataPath, "ScottWisper", "Vocabulary");
            
            Directory.CreateDirectory(_dataDirectory);
            
            _packs = new List<VocabularyPack>();
            
            // Initialize built-in packs
            InitializeBuiltInPacks();
            
            // Load custom vocabulary
            _ = LoadCustomVocabularyAsync();
            
            _logger.LogInformation("VocabularyService initialized with {PackCount} built-in packs", _packs.Count);
        }
        
        /// <summary>
        /// Initialize built-in vocabulary packs
        /// </summary>
        private void InitializeBuiltInPacks()
        {
            // Medical vocabulary pack
            var medicalPack = new VocabularyPack
            {
                Id = "medical-v1",
                Name = "Medical Terminology",
                Description = "Comprehensive medical terminology for healthcare professionals",
                Industry = VocabularyIndustry.Medical,
                Version = "1.0",
                Priority = 150,
                Terms = GetMedicalTerms(),
                CommonPhrases = GetMedicalPhrases()
            };
            
            // Legal vocabulary pack
            var legalPack = new VocabularyPack
            {
                Id = "legal-v1",
                Name = "Legal Terminology",
                Description = "Legal terminology for lawyers and legal professionals",
                Industry = VocabularyIndustry.Legal,
                Version = "1.0",
                Priority = 150,
                Terms = GetLegalTerms(),
                CommonPhrases = GetLegalPhrases()
            };
            
            _packs.Add(medicalPack);
            _packs.Add(legalPack);
        }
        
        /// <summary>
        /// Get medical terminology
        /// </summary>
        private List<VocabularyTerm> GetMedicalTerms()
        {
            return new List<VocabularyTerm>
            {
                // Anatomy
                new VocabularyTerm { Term = "anterior", Category = "Anatomy", Definition = "Front or forward part" },
                new VocabularyTerm { Term = "posterior", Category = "Anatomy", Definition = "Back or rear part" },
                new VocabularyTerm { Term = "proximal", Category = "Anatomy", Definition = "Closer to the center or point of attachment" },
                new VocabularyTerm { Term = "distal", Category = "Anatomy", Definition = "Farther from the center or point of attachment" },
                new VocabularyTerm { Term = "superior", Category = "Anatomy", Definition = "Above or higher position" },
                new VocabularyTerm { Term = "inferior", Category = "Anatomy", Definition = "Below or lower position" },
                new VocabularyTerm { Term = "lateral", Category = "Anatomy", Definition = "Away from the midline" },
                new VocabularyTerm { Term = "medial", Category = "Anatomy", Definition = "Toward the midline" },
                
                // Cardiology
                new VocabularyTerm { Term = "atrial fibrillation", Category = "Cardiology", Definition = "Irregular heart rhythm", IsHighPriority = true },
                new VocabularyTerm { Term = "myocardial infarction", Category = "Cardiology", Definition = "Heart attack", IsHighPriority = true },
                new VocabularyTerm { Term = "hypertension", Category = "Cardiology", Definition = "High blood pressure", IsHighPriority = true },
                new VocabularyTerm { Term = "tachycardia", Category = "Cardiology", Definition = "Fast heart rate" },
                new VocabularyTerm { Term = "bradycardia", Category = "Cardiology", Definition = "Slow heart rate" },
                new VocabularyTerm { Term = "electrocardiogram", Category = "Cardiology", Definition = "ECG/EKG heart test", IsHighPriority = true },
                
                // Neurology
                new VocabularyTerm { Term = "cerebrovascular accident", Category = "Neurology", Definition = "Stroke", IsHighPriority = true },
                new VocabularyTerm { Term = "transient ischemic attack", Category = "Neurology", Definition = "Mini-stroke/TIA" },
                new VocabularyTerm { Term = "seizure", Category = "Neurology", Definition = "Sudden electrical disturbance in brain" },
                new VocabularyTerm { Term = "encephalopathy", Category = "Neurology", Definition = "Brain disease or damage" },
                new VocabularyTerm { Term = "neuropathy", Category = "Neurology", Definition = "Nerve damage" },
                
                // Oncology
                new VocabularyTerm { Term = "malignant", Category = "Oncology", Definition = "Cancerous" },
                new VocabularyTerm { Term = "benign", Category = "Oncology", Definition = "Non-cancerous" },
                new VocabularyTerm { Term = "metastasis", Category = "Oncology", Definition = "Spread of cancer" },
                new VocabularyTerm { Term = "biopsy", Category = "Oncology", Definition = "Tissue sample for examination" },
                new VocabularyTerm { Term = "chemotherapy", Category = "Oncology", Definition = "Cancer treatment with drugs" },
                
                // Radiology
                new VocabularyTerm { Term = "radiograph", Category = "Radiology", Definition = "X-ray image" },
                new VocabularyTerm { Term = "computed tomography", Category = "Radiology", Definition = "CT scan" },
                new VocabularyTerm { Term = "magnetic resonance imaging", Category = "Radiology", Definition = "MRI scan" },
                new VocabularyTerm { Term = "ultrasound", Category = "Radiology", Definition = "Imaging using sound waves" },
                new VocabularyTerm { Term = "contrast", Category = "Radiology", Definition = "Dye used to enhance imaging" },
                
                // Laboratory
                new VocabularyTerm { Term = "CBC", Category = "Laboratory", Definition = "Complete blood count", IsHighPriority = true },
                new VocabularyTerm { Term = "BMP", Category = "Laboratory", Definition = "Basic metabolic panel" },
                new VocabularyTerm { Term = "CMP", Category = "Laboratory", Definition = "Comprehensive metabolic panel" },
                new VocabularyTerm { Term = "lipid panel", Category = "Laboratory", Definition = "Cholesterol test" },
                new VocabularyTerm { Term = "HgbA1c", Category = "Laboratory", Definition = "Hemoglobin A1c/blood sugar test" },
                
                // General Medical
                new VocabularyTerm { Term = "auscultation", Category = "General", Definition = "Listening to body sounds" },
                new VocabularyTerm { Term = "palpation", Category = "General", Definition = "Physical examination by touch" },
                new VocabularyTerm { Term = "percussion", Category = "General", Definition = "Tapping body to assess organs" },
                new VocabularyTerm { Term = "inspection", Category = "General", Definition = "Visual examination" },
                new VocabularyTerm { Term = "differential diagnosis", Category = "General", Definition = "Process of distinguishing diseases" },
                new VocabularyTerm { Term = "etiology", Category = "General", Definition = "Cause or origin of disease" },
                new VocabularyTerm { Term = "pathophysiology", Category = "General", Definition = "Functional changes from disease" },
                new VocabularyTerm { Term = "prognosis", Category = "General", Definition = "Expected outcome or course" },
                
                // Pharmacology
                new VocabularyTerm { Term = "analgesic", Category = "Pharmacology", Definition = "Pain reliever" },
                new VocabularyTerm { Term = "antibiotic", Category = "Pharmacology", Definition = "Bacteria-fighting medication" },
                new VocabularyTerm { Term = "anticoagulant", Category = "Pharmacology", Definition = "Blood thinner" },
                new VocabularyTerm { Term = "antihypertensive", Category = "Pharmacology", Definition = "Blood pressure medication" },
                new VocabularyTerm { Term = "corticosteroid", Category = "Pharmacology", Definition = "Anti-inflammatory steroid" },
                
                // Conditions
                new VocabularyTerm { Term = "diabetes mellitus", Category = "Conditions", Definition = "Diabetes", IsHighPriority = true },
                new VocabularyTerm { Term = "chronic obstructive pulmonary disease", Category = "Conditions", Definition = "COPD" },
                new VocabularyTerm { Term = "congestive heart failure", Category = "Conditions", Definition = "CHF" },
                new VocabularyTerm { Term = "chronic kidney disease", Category = "Conditions", Definition = "CKD" },
                new VocabularyTerm { Term = "gastroesophageal reflux disease", Category = "Conditions", Definition = "GERD" },
                new VocabularyTerm { Term = "upper respiratory infection", Category = "Conditions", Definition = "URI" },
                new VocabularyTerm { Term = "urinary tract infection", Category = "Conditions", Definition = "UTI" },
                new VocabularyTerm { Term = "pneumonia", Category = "Conditions", Definition = "Lung infection", IsHighPriority = true },
            };
        }
        
        /// <summary>
        /// Get medical phrases
        /// </summary>
        private List<string> GetMedicalPhrases()
        {
            return new List<string>
            {
                "chief complaint",
                "history of present illness",
                "review of systems",
                "past medical history",
                "family history",
                "social history",
                "physical examination",
                "vital signs",
                "blood pressure",
                "heart rate",
                "respiratory rate",
                "oxygen saturation",
                "body mass index",
                "assessment and plan",
                "differential diagnosis",
                "patient presents with",
                "pertinent negatives",
                "pertinent positives"
            };
        }
        
        /// <summary>
        /// Get legal terminology
        /// </summary>
        private List<VocabularyTerm> GetLegalTerms()
        {
            return new List<VocabularyTerm>
            {
                // Litigation
                new VocabularyTerm { Term = "plaintiff", Category = "Litigation", Definition = "Person who brings a case to court" },
                new VocabularyTerm { Term = "defendant", Category = "Litigation", Definition = "Person accused in court" },
                new VocabularyTerm { Term = "complaint", Category = "Litigation", Definition = "Formal legal document starting lawsuit" },
                new VocabularyTerm { Term = "answer", Category = "Litigation", Definition = "Defendant's response to complaint" },
                new VocabularyTerm { Term = "motion", Category = "Litigation", Definition = "Request for court order" },
                new VocabularyTerm { Term = "brief", Category = "Litigation", Definition = "Written legal argument" },
                new VocabularyTerm { Term = "deposition", Category = "Litigation", Definition = "Sworn out-of-court testimony" },
                new VocabularyTerm { Term = "subpoena", Category = "Litigation", Definition = "Order to appear or produce documents" },
                new VocabularyTerm { Term = "discovery", Category = "Litigation", Definition = "Pre-trial evidence exchange" },
                new VocabularyTerm { Term = "interrogatory", Category = "Litigation", Definition = "Written questions in discovery" },
                new VocabularyTerm { Term = "affidavit", Category = "Litigation", Definition = "Sworn written statement" },
                new VocabularyTerm { Term = "testimony", Category = "Litigation", Definition = "Evidence given under oath" },
                
                // Court
                new VocabularyTerm { Term = "jurisdiction", Category = "Court", Definition = "Court's authority" },
                new VocabularyTerm { Term = "venue", Category = "Court", Definition = "Appropriate court location" },
                new VocabularyTerm { Term = "statute", Category = "Court", Definition = "Written law" },
                new VocabularyTerm { Term = "precedent", Category = "Court", Definition = "Previous case used as example" },
                new VocabularyTerm { Term = "jurisdiction", Category = "Court", Definition = "Court's power to hear case" },
                new VocabularyTerm { Term = "litigation", Category = "Court", Definition = "Process of taking legal action" },
                new VocabularyTerm { Term = "adjudication", Category = "Court", Definition = "Formal court decision" },
                new VocabularyTerm { Term = "injunction", Category = "Court", Definition = "Court order to do or stop something" },
                new VocabularyTerm { Term = "subpoena", Category = "Court", Definition = "Legal summons" },
                
                // Contract
                new VocabularyTerm { Term = "contract", Category = "Contract", Definition = "Legally binding agreement" },
                new VocabularyTerm { Term = "agreement", Category = "Contract", Definition = "Mutual understanding" },
                new VocabularyTerm { Term = "consideration", Category = "Contract", Definition = "Something of value exchanged" },
                new VocabularyTerm { Term = "breach", Category = "Contract", Definition = "Violation of contract" },
                new VocabularyTerm { Term = "damages", Category = "Contract", Definition = "Monetary compensation" },
                new VocabularyTerm { Term = "liability", Category = "Contract", Definition = "Legal responsibility" },
                new VocabularyTerm { Term = "indemnification", Category = "Contract", Definition = "Compensation for harm" },
                new VocabularyTerm { Term = "warranty", Category = "Contract", Definition = "Guarantee" },
                new VocabularyTerm { Term = "covenant", Category = "Contract", Definition = "Formal promise" },
                new VocabularyTerm { Term = "force majeure", Category = "Contract", Definition = "Unforeseeable circumstances" },
                new VocabularyTerm { Term = "arbitration", Category = "Contract", Definition = "Alternative dispute resolution" },
                new VocabularyTerm { Term = "mediation", Category = "Contract", Definition = "Non-binding dispute resolution" },
                
                // Criminal
                new VocabularyTerm { Term = "felony", Category = "Criminal", Definition = "Serious crime" },
                new VocabularyTerm { Term = "misdemeanor", Category = "Criminal", Definition = "Minor crime" },
                new VocabularyTerm { Term = "indictment", Category = "Criminal", Definition = "Formal charge" },
                new VocabularyTerm { Term = "arraignment", Category = "Criminal", Definition = "Initial court appearance" },
                new VocabularyTerm { Term = "bail", Category = "Criminal", Definition = "Money for release before trial" },
                new VocabularyTerm { Term = "probable cause", Category = "Criminal", Definition = "Reasonable basis for belief" },
                new VocabularyTerm { Term = "beyond reasonable doubt", Category = "Criminal", Definition = "High standard of proof" },
                new VocabularyTerm { Term = "habeas corpus", Category = "Criminal", Definition = "Right to appear before court" },
                
                // Corporate
                new VocabularyTerm { Term = "incorporation", Category = "Corporate", Definition = "Formation of corporation" },
                new VocabularyTerm { Term = "fiduciary", Category = "Corporate", Definition = "Person with legal duty" },
                new VocabularyTerm { Term = "merger", Category = "Corporate", Definition = "Companies combining" },
                new VocabularyTerm { Term = "acquisition", Category = "Corporate", Definition = "One company buying another" },
                new VocabularyTerm { Term = "due diligence", Category = "Corporate", Definition = "Investigation before agreement" },
                new VocabularyTerm { Term = "intellectual property", Category = "Corporate", Definition = "Intangible creations" },
                new VocabularyTerm { Term = "non-disclosure agreement", Category = "Corporate", Definition = "NDA" },
                new VocabularyTerm { Term = "non-compete agreement", Category = "Corporate", Definition = "Agreement not to compete" },
                
                // Property
                new VocabularyTerm { Term = "conveyance", Category = "Property", Definition = "Transfer of property" },
                new VocabularyTerm { Term = "easement", Category = "Property", Definition = "Right to use property" },
                new VocabularyTerm { Term = "encumbrance", Category = "Property", Definition = "Claim against property" },
                new VocabularyTerm { Term = "lien", Category = "Property", Definition = "Legal claim on property" },
                new VocabularyTerm { Term = "title", Category = "Property", Definition = "Legal ownership" },
                new VocabularyTerm { Term = "deed", Category = "Property", Definition = "Property transfer document" },
                new VocabularyTerm { Term = "mortgage", Category = "Property", Definition = "Property loan" },
                new VocabularyTerm { Term = "escrow", Category = "Property", Definition = "Third-party holding funds" },
                
                // General Legal
                new VocabularyTerm { Term = "tort", Category = "General", Definition = "Civil wrongdoing" },
                new VocabularyTerm { Term = "negligence", Category = "General", Definition = "Failure to exercise care" },
                new VocabularyTerm { Term = "prima facie", Category = "General", Definition = "At first sight" },
                new VocabularyTerm { Term = "stare decisis", Category = "General", Definition = "Stand by things decided" },
                new VocabularyTerm { Term = "habeas corpus", Category = "General", Definition = "Produce the body" },
                new VocabularyTerm { Term = "pro bono", Category = "General", Definition = "Free legal work" },
                new VocabularyTerm { Term = "pro se", Category = "General", Definition = "Representing oneself" },
                new VocabularyTerm { Term = "voir dire", Category = "General", Definition = "Jury selection process" },
                new VocabularyTerm { Term = "sub judice", Category = "General", Definition = "Under judicial consideration" },
                new VocabularyTerm { Term = "res judicata", Category = "General", Definition = "Matter already judged" },
            };
        }
        
        /// <summary>
        /// Get legal phrases
        /// </summary>
        private List<string> GetLegalPhrases()
        {
            return new List<string>
            {
                "whereas",
                "hereinafter",
                "hereinafter referred to as",
                "party of the first part",
                "party of the second part",
                "witnesseth",
                "in witness whereof",
                "mutual covenants",
                "good and valuable consideration",
                "binding upon",
                "successors and assigns",
                "governing law",
                "venue and jurisdiction",
                "severability",
                "entire agreement",
                "time is of the essence"
            };
        }
        
        /// <summary>
        /// Load custom vocabulary from disk
        /// </summary>
        private async Task LoadCustomVocabularyAsync()
        {
            var filePath = Path.Combine(_dataDirectory, "custom-vocabulary.json");
            
            if (File.Exists(filePath))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(filePath);
                    _customVocabulary = JsonSerializer.Deserialize<CustomVocabulary>(json);
                    _logger.LogInformation("Loaded custom vocabulary with {TermCount} terms", 
                        _customVocabulary?.CustomTerms.Count ?? 0);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error loading custom vocabulary");
                }
            }
            
            _customVocabulary ??= new CustomVocabulary();
        }
        
        /// <summary>
        /// Save custom vocabulary to disk
        /// </summary>
        private async Task SaveCustomVocabularyAsync()
        {
            lock (_lock)
            {
                _customVocabulary.ModifiedAt = DateTime.UtcNow;
            }
            
            var filePath = Path.Combine(_dataDirectory, "custom-vocabulary.json");
            var json = JsonSerializer.Serialize(_customVocabulary, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(filePath, json);
        }
        
        #region Vocabulary Pack Management
        
        /// <summary>
        /// Get all vocabulary packs
        /// </summary>
        public Task<List<VocabularyPack>> GetAllPacksAsync()
        {
            return Task.FromResult(_packs.ToList());
        }
        
        /// <summary>
        /// Get enabled packs
        /// </summary>
        public Task<List<VocabularyPack>> GetEnabledPacksAsync()
        {
            return Task.FromResult(_packs.Where(p => p.IsEnabled).ToList());
        }
        
        /// <summary>
        /// Enable a pack
        /// </summary>
        public Task<bool> EnablePackAsync(string packId)
        {
            var pack = _packs.FirstOrDefault(p => p.Id == packId);
            if (pack == null)
                return Task.FromResult(false);
            
            pack.IsEnabled = true;
            pack.ModifiedAt = DateTime.UtcNow;
            
            _logger.LogInformation("Enabled vocabulary pack: {PackName}", pack.Name);
            return Task.FromResult(true);
        }
        
        /// <summary>
        /// Disable a pack
        /// </summary>
        public Task<bool> DisablePackAsync(string packId)
        {
            var pack = _packs.FirstOrDefault(p => p.Id == packId);
            if (pack == null)
                return Task.FromResult(false);
            
            pack.IsEnabled = false;
            pack.ModifiedAt = DateTime.UtcNow;
            
            _logger.LogInformation("Disabled vocabulary pack: {PackName}", pack.Name);
            return Task.FromResult(true);
        }
        
        /// <summary>
        /// Get a specific pack
        /// </summary>
        public Task<VocabularyPack?> GetPackAsync(string packId)
        {
            return Task.FromResult(_packs.FirstOrDefault(p => p.Id == packId));
        }
        
        /// <summary>
        /// Get medical pack
        /// </summary>
        public Task<VocabularyPack?> GetMedicalPackAsync()
        {
            return Task.FromResult(_packs.FirstOrDefault(p => p.Industry == VocabularyIndustry.Medical));
        }
        
        /// <summary>
        /// Get legal pack
        /// </summary>
        public Task<VocabularyPack?> GetLegalPackAsync()
        {
            return Task.FromResult(_packs.FirstOrDefault(p => p.Industry == VocabularyIndustry.Legal));
        }
        
        #endregion
        
        #region Custom Vocabulary Management
        
        /// <summary>
        /// Add a custom term
        /// </summary>
        public async Task<VocabularyTerm> AddCustomTermAsync(string term, string? category = null, string? definition = null)
        {
            var vocabularyTerm = new VocabularyTerm
            {
                Term = term,
                Category = category ?? "Custom",
                Definition = definition ?? string.Empty
            };
            
            lock (_lock)
            {
                _customVocabulary.CustomTerms.Add(vocabularyTerm);
            }
            
            await SaveCustomVocabularyAsync();
            
            _logger.LogInformation("Added custom term: {Term}", term);
            return vocabularyTerm;
        }
        
        /// <summary>
        /// Remove a custom term
        /// </summary>
        public async Task<bool> RemoveCustomTermAsync(string termId)
        {
            lock (_lock)
            {
                var term = _customVocabulary.CustomTerms.FirstOrDefault(t => t.Id == termId);
                if (term == null)
                    return false;
                
                _customVocabulary.CustomTerms.Remove(term);
            }
            
            await SaveCustomVocabularyAsync();
            return true;
        }
        
        /// <summary>
        /// Get all custom terms
        /// </summary>
        public Task<List<VocabularyTerm>> GetCustomTermsAsync()
        {
            return Task.FromResult(_customVocabulary.CustomTerms.ToList());
        }
        
        /// <summary>
        /// Add a proper noun
        /// </summary>
        public async Task AddProperNounAsync(string name)
        {
            lock (_lock)
            {
                if (!_customVocabulary.ProperNouns.Contains(name))
                {
                    _customVocabulary.ProperNouns.Add(name);
                }
            }
            
            await SaveCustomVocabularyAsync();
        }
        
        /// <summary>
        /// Remove a proper noun
        /// </summary>
        public async Task<bool> RemoveProperNounAsync(string name)
        {
            lock (_lock)
            {
                if (!_customVocabulary.ProperNouns.Contains(name))
                    return false;
                
                _customVocabulary.ProperNouns.Remove(name);
            }
            
            await SaveCustomVocabularyAsync();
            return true;
        }
        
        /// <summary>
        /// Add an acronym
        /// </summary>
        public async Task AddAcronymAsync(string acronym, string fullForm)
        {
            lock (_lock)
            {
                _customVocabulary.Acronyms[acronym] = fullForm;
            }
            
            await SaveCustomVocabularyAsync();
        }
        
        #endregion
        
        #region Transcription Enhancement
        
        /// <summary>
        /// Get context string for transcription
        /// </summary>
        public async Task<string> GetTranscriptionContextAsync()
        {
            var enabledPacks = await GetEnabledPacksAsync();
            var contextTerms = new List<string>();
            
            // Add terms from enabled packs
            foreach (var pack in enabledPacks)
            {
                var packTerms = pack.Terms
                    .Where(t => t.IsHighPriority)
                    .Select(t => t.Term)
                    .ToList();
                contextTerms.AddRange(packTerms);
            }
            
            // Add custom terms
            lock (_lock)
            {
                contextTerms.AddRange(_customVocabulary.CustomTerms.Select(t => t.Term));
                contextTerms.AddRange(_customVocabulary.ProperNouns);
                contextTerms.AddRange(_customVocabulary.Acronyms.Keys);
                contextTerms.AddRange(_customVocabulary.Acronyms.Values);
            }
            
            return string.Join(", ", contextTerms.Distinct());
        }
        
        /// <summary>
        /// Enhance transcription with vocabulary corrections
        /// </summary>
        public async Task<VocabularyEnhancedResult> EnhanceTranscriptionAsync(string transcription)
        {
            var result = new VocabularyEnhancedResult
            {
                OriginalText = transcription,
                EnhancedText = transcription
            };
            
            if (string.IsNullOrWhiteSpace(transcription))
                return result;
            
            var enabledPacks = await GetEnabledPacksAsync();
            
            // Check each pack for corrections
            foreach (var pack in enabledPacks)
            {
                foreach (var term in pack.Terms)
                {
                    // Check for exact match (case insensitive)
                    if (transcription.IndexOf(term.Term, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        result.ContributingPackIds.Add(pack.Id);
                    }
                    
                    // Check for common misspellings
                    foreach (var misspelling in term.CommonMisspellings)
                    {
                        if (result.EnhancedText.IndexOf(misspelling, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            result.EnhancedText = ReplaceCaseInsensitive(result.EnhancedText, misspelling, term.Term);
                            result.Corrections.Add(new VocabularyCorrection
                            {
                                Original = misspelling,
                                Corrected = term.Term,
                                Source = pack.Name,
                                Type = GetCorrectionType(pack.Industry)
                            });
                        }
                    }
                }
            }
            
            // Apply custom term corrections
            lock (_lock)
            {
                foreach (var term in _customVocabulary.CustomTerms)
                {
                    if (result.EnhancedText.IndexOf(term.Term, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        result.ContributingPackIds.Add("custom");
                    }
                }
                
                // Apply proper noun capitalization
                foreach (var noun in _customVocabulary.ProperNouns)
                {
                    if (result.EnhancedText.IndexOf(noun, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        result.EnhancedText = ReplaceCaseInsensitive(result.EnhancedText, noun, noun);
                    }
                }
            }
            
            result.ContributingPackIds = result.ContributingPackIds.Distinct().ToList();
            return result;
        }
        
        /// <summary>
        /// Replace text case-insensitively while preserving original case
        /// </summary>
        private string ReplaceCaseInsensitive(string text, string oldValue, string newValue)
        {
            // Simple implementation - in production, this would be more sophisticated
            return text.Replace(oldValue, newValue, StringComparison.OrdinalIgnoreCase);
        }
        
        /// <summary>
        /// Get correction type from industry
        /// </summary>
        private CorrectionType GetCorrectionType(VocabularyIndustry industry)
        {
            return industry switch
            {
                VocabularyIndustry.Medical => CorrectionType.MedicalTerm,
                VocabularyIndustry.Legal => CorrectionType.LegalTerm,
                VocabularyIndustry.Technical => CorrectionType.TechnicalTerm,
                _ => CorrectionType.Spelling
            };
        }
        
        #endregion
        
        #region Import/Export
        
        /// <summary>
        /// Get vocabulary statistics
        /// </summary>
        public Task<VocabularyStatistics> GetStatisticsAsync()
        {
            var stats = new VocabularyStatistics();
            
            lock (_lock)
            {
                stats.EnabledPacksCount = _packs.Count(p => p.IsEnabled);
                stats.CustomTermsCount = _customVocabulary.CustomTerms.Count;
            }
            
            return Task.FromResult(stats);
        }
        
        /// <summary>
        /// Import vocabulary from file
        /// </summary>
        public async Task<bool> ImportVocabularyAsync(string filePath)
        {
            try
            {
                var json = await File.ReadAllTextAsync(filePath);
                var imported = JsonSerializer.Deserialize<CustomVocabulary>(json);
                
                if (imported == null)
                    return false;
                
                lock (_lock)
                {
                    _customVocabulary.CustomTerms.AddRange(imported.CustomTerms);
                    _customVocabulary.ProperNouns.AddRange(imported.ProperNouns);
                    foreach (var kvp in imported.Acronyms)
                    {
                        _customVocabulary.Acronyms[kvp.Key] = kvp.Value;
                    }
                }
                
                await SaveCustomVocabularyAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing vocabulary from {FilePath}", filePath);
                return false;
            }
        }
        
        /// <summary>
        /// Export vocabulary to file
        /// </summary>
        public async Task<string> ExportVocabularyAsync(string? filePath = null)
        {
            filePath ??= Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                $"ScottWisper_Vocabulary_{DateTime.Now:yyyyMMdd_HHmmss}.json"
            );
            
            CustomVocabulary exportData;
            lock (_lock)
            {
                exportData = new CustomVocabulary
                {
                    CustomTerms = _customVocabulary.CustomTerms.ToList(),
                    ProperNouns = _customVocabulary.ProperNouns.ToList(),
                    Acronyms = new Dictionary<string, string>(_customVocabulary.Acronyms)
                };
            }
            
            var json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(filePath, json);
            
            return filePath;
        }
        
        /// <summary>
        /// Reset to defaults
        /// </summary>
        public async Task ResetToDefaultsAsync()
        {
            // Disable all packs
            foreach (var pack in _packs)
            {
                pack.IsEnabled = false;
            }
            
            // Clear custom vocabulary
            lock (_lock)
            {
                _customVocabulary = new CustomVocabulary();
            }
            
            await SaveCustomVocabularyAsync();
            
            _logger.LogInformation("Vocabulary reset to defaults");
        }
        
        #endregion
    }
}
