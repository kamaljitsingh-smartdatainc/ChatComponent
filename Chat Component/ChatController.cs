using HC.Model;
using HC.Patient.Model.Chat;
using HC.Patient.Model.Common;
using HC.Patient.Model.Patient;
using HC.Patient.Service.IServices.Chats;
using HC.Patient.Service.IServices.Patient;
using HC.Patient.Service.Token;
using HC.Patient.Service.Token.Interfaces;
using HC.Patient.Web.Hubs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace HC.Patient.Web.Controllers
{
    [Produces("application/json")]
    [Route("Chat")]
    public class ChatController : BaseController
    {
        private readonly IChatService _chatService;
        private readonly IHubContext<ChatHub> _hubContext;
        private readonly ITokenService _tokenService;
        private readonly IPatientService _patientService;
        private readonly IStaffService _staffService;
        public ChatController(IChatService chatService, IHubContext<ChatHub> hub, ITokenService tokenService, IPatientService patientService, IStaffService staffService)
        {
            _chatService = chatService;
            _hubContext = hub;
            _tokenService = tokenService;
            _patientService = patientService;
            _staffService = staffService;
        }

        
        [HttpGet]
        [Route("GetChatHistory")]
        public JsonResult GetChatHistory(ChatParmModel chatParmModel)
        {
            TokenModel token = GetToken(HttpContext);
            JsonModel data = _chatService.ExecuteFunctions<JsonModel>(() => _chatService.GetChatHistory(chatParmModel, GetToken(HttpContext)));

            if (chatParmModel != null && chatParmModel.FromUserId > 0)
            {
                JsonModel notificationModel = _tokenService.GetChatAndNotificationCount(chatParmModel.FromUserId, token);
                string connId = _chatService.GetConnectionId(chatParmModel.FromUserId);
                if (connId != null && notificationModel != null)
                {
                    _hubContext.Clients.Client(connId).SendAsync("MobileNotificationResponse", notificationModel);
                }
            }

            return Json(data);
        }

        [HttpGet]
        [Route("UpdateChatForPOC")]
        public JsonResult UpdateChatForPOC(int ChatId, string Confirmtype)
        {
            int PCMID;
            TokenModel token = GetToken(HttpContext);
            JsonModel data = _chatService.ExecuteFunctions<JsonModel>(() => _chatService.UpdateChat(ChatId, Confirmtype, GetToken(HttpContext)));
            JsonModel dataN = _tokenService.UpdateNotificationForPOC(ChatId, Confirmtype, GetToken(HttpContext), out PCMID);
            if (dataN.data != null)
            {
                JsonModel patient = _patientService.GetPatientById(System.Convert.ToInt32(dataN.data), token);

                if (Confirmtype == "Yes" && PCMID > 0)
                {
                    var stf = _staffService.GetStaffByUserId(PCMID);
                    if (stf != null)
                    {
                        NotificationModel notificationModel = _tokenService.GetLoginNotification(stf.UserID, GetToken(HttpContext));
                        string connectionId = _chatService.GetConnectionId(stf.UserID);
                        if (!string.IsNullOrEmpty(connectionId))
                        {
                            _hubContext.Clients.Client(connectionId).SendAsync("NotificationResponse", notificationModel);
                        }
                    }
                }

                if (patient != null)
                {
                    PatientDemographicsModel patientData = (PatientDemographicsModel)patient.data;

                    if (patient != null && patientData.UserID > 0)
                    {
                        JsonModel notificationModel = _tokenService.GetChatAndNotificationCount(patientData.UserID, token);
                        string connId = _chatService.GetConnectionId(patientData.UserID);
                        if (connId != null)
                        {
                            _hubContext.Clients.Client(connId).SendAsync("MobileNotificationResponse", notificationModel);
                        }
                    }
                }
            }

            return Json(data);
        }
    }
}