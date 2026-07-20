using LaunchDarklyPOC.Shared.Models;
using Microsoft.AspNetCore.Mvc;

namespace LaunchDarklyPOC.DotNetMiddleware.Controllers;

[ApiController]
[Route("[controller]")]
public class ProcessController : ControllerBase
{
    private readonly ILogger<ProcessController> _logger;

    public ProcessController(ILogger<ProcessController> logger)
    {
        _logger = logger;
    }

    [HttpPost]
    [ProducesResponseType(typeof(ProcessResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<ProcessResponse> Post([FromBody] ProcessRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TransactionId))
            return BadRequest("TransactionId is required.");

        _logger.LogInformation(
            "DotNetMiddleware (NEW) processing request | AccountId={AccountId} TransactionId={TransactionId} " +
            "CorrelationId={CorrelationId} Operation={Operation} Amount={Amount} {Currency} RoutedFrom={RoutedFrom}",
            request.AccountId, request.TransactionId, request.CorrelationId,
            request.Operation, request.Amount, request.Currency, request.RoutedFrom);

        var response = new ProcessResponse
        {
            TransactionId = request.TransactionId,
            Status = "SUCCESS",
            ProcessedBy = "DotNetMiddleware",
            ProcessedAt = DateTime.UtcNow,
            Message = $"Transaction {request.TransactionId} processed successfully by .NET Middleware (new system)"
        };

        _logger.LogInformation(
            "DotNetMiddleware completed | TransactionId={TransactionId} Status={Status}",
            response.TransactionId, response.Status);

        return Ok(response);
    }
}
