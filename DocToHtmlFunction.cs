using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace FileConverter
{
    public class DocToHtmlFunction
    {
        private readonly ILogger<DocToHtmlFunction> _logger;

        public DocToHtmlFunction(ILogger<DocToHtmlFunction> log)
        {
            _logger = log;
        }

        [FunctionName("DocToHtml")]
        [OpenApiOperation(operationId: "Run", tags: new[] { "name" })]
        [OpenApiParameter(name: "name", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "The **Name** parameter")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/plain", bodyType: typeof(string), Description = "The OK response")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            var file = req.Form.Files.GetFile("file");
            if (file == null)
            {
                return new BadRequestObjectResult("File not found in request");
            }

            var inputPath = Path.GetTempFileName();
            var outputPath = Path.GetTempFileName();
            await using (var inputStream = file.OpenReadStream())
            {
                await using (var outputStream = File.OpenWrite(inputPath))
                {
                    await inputStream.CopyToAsync(outputStream);
                }
            }

            // If 'self-contained' arg is deprecated, use 'embed-resources' instead
            var arguments = string.Format(" --standalone --self-contained -f docx -t html -o {1} {0}", inputPath, outputPath);

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "pandoc",
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
            };

            process.Start();
            await process.WaitForExitAsync();
            var exitCode = process.ExitCode;
            var error = await process.StandardError.ReadToEndAsync();
            if (exitCode != 0)
            {
                return new BadRequestObjectResult(error);
            }

            return new FileStreamResult(File.OpenRead(outputPath), "text/html");
        }
    }
}
