using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace PL.ViewModels.Documents
{
    /// <summary>
    /// Represents the upload form fields for a learning document, including the subject mapping.
    /// </summary>
    public class DocumentUploadViewModel
    {
        [Required(ErrorMessage = "Please select one or more files to upload.")]
        [Display(Name = "Files")]
        public System.Collections.Generic.List<IFormFile> Files { get; set; } = new System.Collections.Generic.List<IFormFile>();

        [Required]
        public Guid SubjectId { get; set; }
    }
}
