using MakeUpServiceApi.AgentTools;
using MakeUpServiceApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;

namespace MakeUpServiceApi.Controllers.UserControllers
{
    [ApiController]
    [Route("api/chat")]
    public class ChatUserController : ControllerBase
    {

        // dto
        public class ChatRequestDto
        {
            public string NewMessage { get; set; }
            public List<ChatHistory> History { get; set; } = new List<ChatHistory>();
        }
        public class ChatHistory
        {
            public string Role { get; set; }
            public string Text { get; set; }
        }
        private readonly AppDbContext _db;
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;
        private readonly ILogger<ChatUserController> _logger;
        private readonly AgentTools.AgentTools _agentTools;
        public ChatUserController(AppDbContext db, HttpClient httpClient, IConfiguration configuration, ILogger<ChatUserController> logger, AgentTools.AgentTools agentTools)
        {
            _db = db;
            _httpClient = httpClient;
            _config = configuration;
            _logger = logger;
            _agentTools = agentTools;
        }
        private string SystemPrompt(List<Service> services)
        {
            var prompt = new StringBuilder();

            // ============================================================================
            // SECTION 1: BASIC IDENTITY & CONTEXT
            // ============================================================================
            prompt.AppendLine("╔════════════════════════════════════════════════════════════════╗");
            prompt.AppendLine("║                 Shirley AI BOOKING ASSISTANT                   ║");
            prompt.AppendLine("║           Representing Makeup Artist: Shirley 💄               ║");
            prompt.AppendLine("╚════════════════════════════════════════════════════════════════╝");
            prompt.AppendLine();
            prompt.AppendLine($"Malaysia current time: {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")} | Currency: MYR");
            prompt.AppendLine();
            prompt.AppendLine("You are Shirley's exclusive AI booking assistant,     makeup artist Shirley.");
            prompt.AppendLine("Your role is to help customers book makeup services, answer questions, and provide excellent customer service.");
            prompt.AppendLine();
            prompt.AppendLine("🌍 CRITICAL LANGUAGE REQUIREMENT:");
            prompt.AppendLine("  • If user writes in Mandarin/Chinese → Reply in Mandarin/Chinese");
            prompt.AppendLine("  • If user writes in English/Manglish → Reply in English/Manglish");
            prompt.AppendLine("  • If user writes in Malay → Reply in Malay");
            prompt.AppendLine("  • Always match the user's language for better communication!");
            prompt.AppendLine();

            // ============================================================================
            // SECTION 2: AVAILABLE SERVICES
            // ============================================================================
            if (services != null && services.Any())
            {
                prompt.AppendLine("┌────────────────────────────────────────────────────────────────┐");
                prompt.AppendLine("│           💅 AVAILABLE MAKEUP SERVICES (Updated)               │");
                prompt.AppendLine("└────────────────────────────────────────────────────────────────┘");
                prompt.AppendLine();
                foreach (var service in services)
                {
                    prompt.AppendLine($"  📍 {service.Name}");
                    prompt.AppendLine($"     Price: RM{service.Price:F2}");
                    prompt.AppendLine($"     Duration: {service.EstimatedDurationMinutes} minutes");
                    prompt.AppendLine($"     Details: {service.Description}");
                    prompt.AppendLine();
                }
            }
            else
            {
                prompt.AppendLine("⚠️  Currently, there are no available services. Please check back later!");
                prompt.AppendLine();
            }

            // ============================================================================
            // SECTION 3: AVAILABLE TOOLS & CAPABILITIES
            // ============================================================================
            prompt.AppendLine("┌────────────────────────────────────────────────────────────────┐");
            prompt.AppendLine("│              🛠️  AVAILABLE TOOLS & CAPABILITIES                 │");
            prompt.AppendLine("└────────────────────────────────────────────────────────────────┘");
            prompt.AppendLine();
            prompt.AppendLine("You can call MULTIPLE tools in sequence to provide comprehensive assistance:");
            prompt.AppendLine();
            prompt.AppendLine("  1️⃣  GetService");
            prompt.AppendLine("      → Retrieve all active makeup services with details");
            prompt.AppendLine("      → Use when: User asks about available services, pricing, or service options");
            prompt.AppendLine();
            prompt.AppendLine();
            prompt.AppendLine("  3️⃣  CheckScheduleBlocker");
            prompt.AppendLine("      → Check for blocked/unavailable dates in a specific month");
            prompt.AppendLine("      → Use when: User asks about availability, blocked dates, or when they should NOT book");
            prompt.AppendLine();
            prompt.AppendLine("  4️⃣  CheckBookingSchedule");
            prompt.AppendLine("      → Check existing bookings and availability for a specific date");
            prompt.AppendLine("      → Use when: User wants to know available time slots on a particular date");
            prompt.AppendLine();
            prompt.AppendLine("  5️⃣  CalculatePriceAndTravelFee");
            prompt.AppendLine("      → Calculate total price (service + travel fee) based on location");
            prompt.AppendLine("      → Use when: User asks about pricing, travel charges, or final total cost");
            prompt.AppendLine();
            prompt.AppendLine("  6️⃣  CreateBooking");
            prompt.AppendLine("      → Create a new booking once user is ready");
            prompt.AppendLine("      → Required info: name, phone number, date, time, location, service ID");
            prompt.AppendLine("      → Use when: User confirms they want to proceed with booking");
            prompt.AppendLine();

            // ============================================================================
            // SECTION 4: BOOKING WORKFLOW & RULES
            // ============================================================================
            prompt.AppendLine("┌────────────────────────────────────────────────────────────────┐");
            prompt.AppendLine("│              📋 BOOKING WORKFLOW & SMART RULES                   │");
            prompt.AppendLine("└────────────────────────────────────────────────────────────────┘");
            prompt.AppendLine();
            prompt.AppendLine("STEP 1 - REQUIREMENT GATHERING (Do this step-by-step, NOT all at once!):");
            prompt.AppendLine("  ✓ Ask what service they're interested in (use GetService if unsure)");
            prompt.AppendLine("  ✓ Ask for their preferred date and time");
            prompt.AppendLine("  ✓ Confirm full location/address. If the address is too brief (e.g., just 'KL'), politely ask for the full specific address before calculating fees.");
            prompt.AppendLine("  ✓ Get their name and phone number");
            prompt.AppendLine();
            prompt.AppendLine("STEP 3 - CONFIRMATION:");
            prompt.AppendLine("  ✓ Show user the complete summary with:");
            prompt.AppendLine("    - Service name & description");
            prompt.AppendLine("    - Date & time");
            prompt.AppendLine("    - Location");
            prompt.AppendLine("    - Price breakdown (Service Price | Travel Fee | Total)");
            prompt.AppendLine("  ✓ Ask user to explicitly confirm ALL details AND the final total price before you create the booking.");
            prompt.AppendLine();
            prompt.AppendLine("STEP 4 - BOOKING CREATION:");
            prompt.AppendLine("  ✓ Once confirmed, call CreateBooking with complete information");
            prompt.AppendLine("  ✓ Show confirmation message with booking details");
            prompt.AppendLine();

            // ============================================================================
            // SECTION 5: BUSINESS RULES & BOUNDARIES
            // ============================================================================
            prompt.AppendLine("┌────────────────────────────────────────────────────────────────┐");
            prompt.AppendLine("│            ⚠️  BUSINESS RULES & IMPORTANT BOUNDARIES            │");
            prompt.AppendLine("└────────────────────────────────────────────────────────────────┘");
            prompt.AppendLine();
            prompt.AppendLine("✋ WHAT YOU CAN DO:");
            prompt.AppendLine("  ✓ Book new appointments");
            prompt.AppendLine("  ✓ Provide service details and pricing");
            prompt.AppendLine("  ✓ Check availability and schedule");
            prompt.AppendLine("  ✓ Calculate travel fees based on location");
            prompt.AppendLine();
            prompt.AppendLine("❌ WHAT YOU CANNOT DO:");
            prompt.AppendLine("  ✗ Cancel existing bookings → Direct to WhatsApp");
            prompt.AppendLine("  ✗ Reschedule existing bookings → Direct to WhatsApp");
            prompt.AppendLine("  ✗ Modify payment or booking terms → Direct to WhatsApp");
            prompt.AppendLine("  ✗ Discuss services not in the system → Politely decline");
            prompt.AppendLine();
            prompt.AppendLine("💰 PRICING RULES:");
            prompt.AppendLine("  • Always show prices in format: RM XXX.XX");
            prompt.AppendLine("  • Always break down: Service Price + Travel Fee = Total");
            prompt.AppendLine("  • Example: RM 100.00 (service) + RM 20.50 (travel) = RM 120.50 (total)");
            prompt.AppendLine();
            prompt.AppendLine("📅 AVAILABILITY RULES:");
            prompt.AppendLine("  • MUST check CheckScheduleBlocker AND CheckBookingSchedule before confirming");
            prompt.AppendLine("  • If date is blocked → Suggest alternative dates");
            prompt.AppendLine("  • If time slot is booked → Suggest other available times");
            prompt.AppendLine("  • If date/time unavailable → DO NOT proceed with booking");
            prompt.AppendLine();
            prompt.AppendLine("📞 CONTACT FOR SPECIAL REQUESTS:");
            prompt.AppendLine("  If customer needs special requests or custom services:");
            prompt.AppendLine("  → Direct them to message Shirley on WhatsApp for discussion");
            prompt.AppendLine();

            // ============================================================================
            // SECTION 6: CONVERSATION STYLE & TONE
            // ============================================================================
            prompt.AppendLine("┌────────────────────────────────────────────────────────────────┐");
            prompt.AppendLine("│         🎨 CONVERSATION STYLE, TONE & EMOJI USAGE               │");
            prompt.AppendLine("└────────────────────────────────────────────────────────────────┘");
            prompt.AppendLine();
            prompt.AppendLine("TONE: Friendly, professional, warm, and helpful");
            prompt.AppendLine("  • Use casual language (not too formal)");
            prompt.AppendLine("  • Use Malaysian English expressions naturally");
            prompt.AppendLine("  • Be encouraging and enthusiastic about Shirley's services");
            prompt.AppendLine();
            prompt.AppendLine("EMOJI USAGE: Use emojis strategically to make responses engaging 💫");
            prompt.AppendLine("  • 💄 For makeup/beauty services");
            prompt.AppendLine("  • 💅 For nail/finishing touches");
            prompt.AppendLine("  • 📅 For dates and scheduling");
            prompt.AppendLine("  • ⏰ For time-related info");
            prompt.AppendLine("  • 💰 For pricing");
            prompt.AppendLine("  • ✅ For confirmations");
            prompt.AppendLine("  • ⚠️  For warnings or issues");
            prompt.AppendLine("  • 😊 For friendliness");
            prompt.AppendLine();
            prompt.AppendLine("DO NOT use excessive emojis - keep it professional!");
            prompt.AppendLine();
            prompt.AppendLine("PERSONALIZATION:");
            prompt.AppendLine("  • Address customer by name when they provide it");
            prompt.AppendLine("  • Show genuine interest in their needs");
            prompt.AppendLine("  • Provide recommendations based on their situation");
            prompt.AppendLine("  • Make them feel valued and special ✨");
            prompt.AppendLine();

            // ============================================================================
            // SECTION 7: CONVERSATION FLOW EXAMPLES
            // ============================================================================
            prompt.AppendLine("┌────────────────────────────────────────────────────────────────┐");
            prompt.AppendLine("│         💬 CONVERSATION FLOW EXAMPLES (For Reference)           │");
            prompt.AppendLine("└────────────────────────────────────────────────────────────────┘");
            prompt.AppendLine();
            prompt.AppendLine("EXAMPLE 1 - New Booking Inquiry:");
            prompt.AppendLine("  User: \"Hi, I want to book makeup services\"");
            prompt.AppendLine("  You: \"Hi! 😊 Welcome to Shirley Makeup! I'd love to help you book with Shirley. \"");
            prompt.AppendLine("       \"What kind of makeup service are you interested in? We offer:\"");
            prompt.AppendLine("       [Call GetService] → \"Show the services here\"");
            prompt.AppendLine("       \"Which one catches your eye? 💄\"");
            prompt.AppendLine();
            prompt.AppendLine("EXAMPLE 2 - Date Selection:");
            prompt.AppendLine("  User: \"I want June 15th at 2PM\"");
            prompt.AppendLine("  You: \"Perfect! Let me check if that date is available for you... ⏰\"");
            prompt.AppendLine("       [Call CheckScheduleBlocker for June] → Check if date is blocked");
            prompt.AppendLine("       [Call CheckBookingSchedule for June 15] → Check if 2PM is available");
            prompt.AppendLine("       \"Great! June 15th at 2PM is available! 😊\"");
            prompt.AppendLine();
            prompt.AppendLine("EXAMPLE 3 - Pricing Query:");
            prompt.AppendLine("  User: \"How much would it cost at Pavilion KL?\"");
            prompt.AppendLine("  You: \"Let me calculate the total for you... 💰\"");
            prompt.AppendLine("       [Call CalculatePriceAndTravelFee]");
            prompt.AppendLine("       \"The total would be: RM 100.00 (service) + RM 25.50 (travel to Pavilion) = RM 125.50\"");
            prompt.AppendLine();

            // ============================================================================
            // SECTION 8: SOCIAL MEDIA & CONTACT LINKS
            // ============================================================================
            prompt.AppendLine("┌────────────────────────────────────────────────────────────────┐");
            prompt.AppendLine("│              📱 SOCIAL MEDIA & CONTACT INFORMATION              │");
            prompt.AppendLine("└────────────────────────────────────────────────────────────────┘");
            prompt.AppendLine();
            prompt.AppendLine("Follow Shirley's work and aesthetic:");
            prompt.AppendLine("  📘 Facebook: https://www.facebook.com/ShirleyMakeup");
            prompt.AppendLine("  📸 Instagram: https://www.instagram.com/shirley.makeup");
            prompt.AppendLine("  🔴 小红书: https://www.xiaohongshu.com/user/profile/ShirleyBeauty");
            prompt.AppendLine("  💬 WhatsApp: Available for custom requests and rescheduling");
            prompt.AppendLine();

            // ============================================================================
            // SECTION 9: FINAL REMINDER
            // ============================================================================
            prompt.AppendLine("╔════════════════════════════════════════════════════════════════╗");
            prompt.AppendLine("║  Remember: You are here to make the booking experience smooth  ║");
            prompt.AppendLine("║            and enjoyable. Always prioritize the customer's    ║");
            prompt.AppendLine("║            satisfaction and represent Shirley professionally! ║");
            prompt.AppendLine("╚════════════════════════════════════════════════════════════════╝");
            prompt.AppendLine();

            return prompt.ToString();
        }
        [HttpPost("send")]
        public async Task<IActionResult> AskAgent([FromBody] ChatRequestDto request)
        {
            try
            {
                var activeServices = await _db.Services.Where(s => s.Status == "Active").ToListAsync();
                string systemPrompt = SystemPrompt(activeServices);

                var toolsDeclaration = new
                {
                    function_declarations = new object[]
                    {
                        // Method Get - GetService
                        new {
                            name = "GetService",
                            description = "Retrieve a list of all active makeup services with their details including name, price, and description.",
                            parameters = new {
                                type = "OBJECT",
                                properties = new { },
                                required = new string[] { }
                            }
                        },
                        // Method Get - CheckScheduleBlocker
                        new {
                            name = "CheckScheduleBlocker",
                            description = "Check for blocked dates/unavailable periods in a specific month and year.",
                            parameters = new {
                                type = "OBJECT",
                                properties = new {
                                    targetMonth = new { type = "STRING", description = "The target month to check for blocked dates (format: YYYY-MM-DD)." }
                                },
                                required = new[] { "targetMonth" }
                            }
                        },
                        // Method Get - CheckBookingSchedule
                        new {
                            name = "CheckBookingSchedule",
                            description = "Check existing bookings for a specific date to see availability. Returns pending and approved bookings.",
                            parameters = new {
                                type = "OBJECT",
                                properties = new {
                                    date = new { type = "STRING", description = "The date to check for existing bookings (format: YYYY-MM-DD)." }
                                },
                                required = new[] { "date" }
                            }
                        },
                        // Method Get - CalculatePriceAndTravelFee
                        new {
                            name = "CalculatePriceAndTravelFee",
                            description = "Calculate the total price including service price and travel fee based on customer location address.",
                            parameters = new {
                                type = "OBJECT",
                                properties = new {
                                    serviceID = new { type = "INTEGER", description = "The ID of the makeup service." },
                                    address = new { type = "STRING", description = "The customer's full location address." }
                                },
                                required = new[] { "serviceID", "address" }
                            }
                        },
                        // Method Post - CreateBooking
                        new {
                            name = "CreateBooking",
                            description = "Create a new booking for a makeup service with customer details and appointment information.",
                            parameters = new {
                                type = "OBJECT",
                                properties = new {
                                    name = new { type = "STRING", description = "Customer's full name." },
                                    phoneNumber = new { type = "STRING", description = "Customer's phone number." },
                                    appointmentDate = new { type = "STRING", description = "The desired appointment date and time (format: YYYY-MM-DD HH:mm:ss)." },
                                    locationAddress = new { type = "STRING", description = "The customer's service location address." },
                                    serviceID = new { type = "INTEGER", description = "The ID of the makeup service to book." }
                                },
                                required = new[] { "name", "phoneNumber", "appointmentDate", "locationAddress", "serviceID" }
                            }
                        }
                    }
                };
                var geminiPayload = new
                {
                    system_instruction = new { parts = new { text = systemPrompt } },
                    contents = new List<object>(),
                    tools = new[] { toolsDeclaration }
                };

                var contentsList = (List<object>)geminiPayload.contents;

                // Limit history to the last 20 messages to prevent token overflow
                var recentHistory = request.History.TakeLast(20).ToList();
                foreach (var msg in recentHistory)
                {
                    contentsList.Add(new
                    {
                        role = msg.Role == "user" ? "user" : "model",
                        parts = new[] { new { text = msg.Text } }
                    });
                }
                contentsList.Add(new
                {
                    role = "user",
                    parts = new[] { new { text = request.NewMessage } }
                });

                string geminiApiKey = _config["GoogleSettings:ApiKey"];
                var modelSetting = await _db.SystemSettings.FindAsync("GeminiModel");
                string selectedModel = !string.IsNullOrEmpty(modelSetting?.Value) ? modelSetting.Value : "gemini-3.1-flash-lite";
                var requestUrl = $"https://generativelanguage.googleapis.com/v1beta/models/{selectedModel}:generateContent?key={geminiApiKey}";

                var firstContent = new StringContent(JsonSerializer.Serialize(geminiPayload), Encoding.UTF8, "application/json");
                var firstResponse = await _httpClient.PostAsync(requestUrl, firstContent);
                var firstResponseJson = await firstResponse.Content.ReadAsStringAsync();

                if (!firstResponse.IsSuccessStatusCode)
                {
                    return StatusCode(500, new { error = "First API call failed", details = firstResponseJson });
                }

                using var jsonDoc = JsonDocument.Parse(firstResponseJson);

                // Maximum rounds of tool calls to prevent infinite loops
                const int maxToolRounds = 5;
                var currentResponseJson = firstResponseJson;
                JsonDocument currentDoc = jsonDoc;

                for (int round = 0; round < maxToolRounds; round++)
                {
                    if (!currentDoc.RootElement.TryGetProperty("candidates", out var candidates) ||
                        candidates.GetArrayLength() == 0)
                    {
                        return StatusCode(500, new { error = "AI response missing candidates.", rawResponse = currentResponseJson });
                    }

                    var contentElement = candidates[0].GetProperty("content");
                    var partsList = contentElement.GetProperty("parts");

                    // Check if any part contains a functionCall
                    bool hasFunctionCall = false;
                    foreach (var p in partsList.EnumerateArray())
                    {
                        if (p.TryGetProperty("functionCall", out _))
                        {
                            hasFunctionCall = true;
                            break;
                        }
                    }

                    if (!hasFunctionCall)
                    {
                        // No function call — extract the text reply
                        foreach (var p in partsList.EnumerateArray())
                        {
                            if (p.TryGetProperty("text", out var textProp))
                            {
                                return Ok(new { reply = textProp.GetString() });
                            }
                        }
                        return BadRequest(new { error = "Unexpected response format from AI." });
                    }

                    // Has function call(s) — execute them all
                    // 1. Preserve the ENTIRE raw model content (includes thought_signature, etc.)
                    contentsList.Add(JsonSerializer.Deserialize<object>(contentElement.GetRawText()));

                    // 2. Execute each function call and collect responses
                    var functionResponseParts = new List<object>();
                    foreach (var p in partsList.EnumerateArray())
                    {
                        if (p.TryGetProperty("functionCall", out var fc))
                        {
                            string toolName = fc.GetProperty("name").GetString() ?? "unknown";
                            var argsDict = new Dictionary<string, object>();
                            if (fc.TryGetProperty("args", out var argsProp))
                            {
                                argsDict = JsonSerializer.Deserialize<Dictionary<string, object>>(argsProp.GetRawText()) ?? new();
                            }

                            _logger.LogInformation("Round {Round}: Gemini requested tool: {ToolName}", round + 1, toolName);

                            var toolCallObj = new ToolCall { Tool = toolName, Args = argsDict };
                            string toolResultJson = await _agentTools.ExecuteToolAsync(toolCallObj);

                            // Deserialize so it doesn't get double-escaped when sent back
                            object resultObj;
                            try { resultObj = JsonSerializer.Deserialize<object>(toolResultJson) ?? toolResultJson; }
                            catch { resultObj = toolResultJson; }

                            functionResponseParts.Add(new
                            {
                                functionResponse = new
                                {
                                    name = toolName,
                                    response = new { result = resultObj }
                                }
                            });
                        }
                    }

                    // 3. Add function responses to the conversation
                    contentsList.Add(new
                    {
                        role = "function",
                        parts = functionResponseParts
                    });

                    // 4. Create new payload with updated contents for next request
                    var nextPayload = new
                    {
                        system_instruction = new { parts = new { text = systemPrompt } },
                        contents = contentsList,
                        tools = new[] { toolsDeclaration }
                    };

                    // 5. Send updated payload back to Gemini
                    var nextContent = new StringContent(JsonSerializer.Serialize(nextPayload), Encoding.UTF8, "application/json");
                    var nextResponse = await _httpClient.PostAsync(requestUrl, nextContent);
                    var nextResponseJson = await nextResponse.Content.ReadAsStringAsync();

                    if (!nextResponse.IsSuccessStatusCode)
                    {
                        _logger.LogError("Gemini API error on round {Round}: {Response}", round + 1, nextResponseJson);
                        return StatusCode(500, new { error = $"AI API call failed on round {round + 1}", details = nextResponseJson });
                    }

                    // Dispose previous doc if not the original (original is disposed by using)
                    if (round > 0) currentDoc.Dispose();

                    currentResponseJson = nextResponseJson;
                    currentDoc = JsonDocument.Parse(nextResponseJson);
                    // Loop back to check if the model wants to call more tools
                }

                // If we exhausted all rounds, try to extract whatever text is available
                if (currentDoc.RootElement.TryGetProperty("candidates", out var finalCandidates) &&
                    finalCandidates.GetArrayLength() > 0)
                {
                    foreach (var p in finalCandidates[0].GetProperty("content").GetProperty("parts").EnumerateArray())
                    {
                        if (p.TryGetProperty("text", out var txt))
                        {
                            return Ok(new { reply = txt.GetString() });
                        }
                    }
                }

                return StatusCode(500, new { error = "AI exceeded maximum tool call rounds without providing a text response." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Internal Server Error", message = ex.Message, stackTrace = ex.ToString() });
            }
        }
    }
}
