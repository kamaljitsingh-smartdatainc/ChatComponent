using HC.Common;
using HC.Common.Options;
using HC.Model;
using HC.Patient.Model.Chat;
using HC.Patient.Model.Message;
using Microsoft.Extensions.Logging;
using HC.Patient.Service.IServices.Chats;
using HC.Patient.Service.IServices.Message;
using HC.Patient.Service.Token.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using System;
using System.Threading.Tasks;
using static HC.Common.Enums.CommonEnum;
using NotificationModel = HC.Patient.Model.NotificationSetting.NotificationModel;
using HC.Patient.Repositories.Interfaces;
using HC.Patient.Service.IServices.Notifications;
using HC.Patient.Repositories.IRepositories.Patient;
using HC.Patient.Entity;
using HC.Patient.Model.MasterData;
using HC.Patient.Service.IServices.MasterData;

namespace HC.Patient.Web.Hubs
{
    public class ChatHub : Hub
    {
        private readonly IChatService _chatService;
        private readonly IMessageService _messageService;
        private readonly ITokenService _tokenService;
        private readonly ILogger _logger;
        private readonly JwtIssuerOptions _jwtOptions;
        private readonly ITokenRepository _tokenRepository;
        private readonly INotificationService _notificationService;
        private readonly IPatientRepository _patientRepository;
        private readonly ILocationService _locationService;
        public ChatHub(IChatService chatService, IMessageService messageService, ITokenService tokenService, ILoggerFactory loggerFactory, 
            IOptions<JwtIssuerOptions> jwtOptions, ITokenRepository tokenRepository, INotificationService notificationService, 
            IPatientRepository patientRepository, ILocationService locationService)
        {
            _chatService = chatService;
            _messageService = messageService;
            _tokenService = tokenService;
            _tokenRepository = tokenRepository;
            _notificationService = notificationService;
            _patientRepository = patientRepository;
            _locationService = locationService;

            _jwtOptions = jwtOptions.Value;
            ThrowIfInvalidOptions(_jwtOptions);
            _logger = loggerFactory.CreateLogger<ChatHub>();
        }

        public override async Task OnConnectedAsync()
        {
            try
            {
                ChatConnectedUserModel chatConnectedUserModel = new ChatConnectedUserModel();
                TokenModel tokenModel = CommonMethods.GetTokenDataModel(Context.GetHttpContext());
                chatConnectedUserModel.ConnectionId = Context.ConnectionId;
                chatConnectedUserModel.UserId = tokenModel.UserID;
                chatConnectedUserModel.IsOnline = true;
                await _chatService.ChatConnectedUser(chatConnectedUserModel, tokenModel);
                chatConnectedUserModel.ConnectionId = null;
                await Clients.All.SendAsync("CheckOnline", chatConnectedUserModel);
            }
            catch (Exception)
            {
                throw new HubException("Error in SignalR Hub OnConnectedAsync method!");
            }
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            ChatConnectedUserModel userModel = await _chatService.UpdateChatUserStatus(Context.ConnectionId, false);
            if (userModel.UserId > 0)
                await Clients.All.SendAsync("CheckOnline", userModel);
            await base.OnDisconnectedAsync(exception);
        }

        /// <summary>
        /// this method is used for connect the user with hub and save the connectionid into database
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public async Task Connect(int userId)
        {
            try
            {
                ChatConnectedUserModel chatConnectedUserModel = new ChatConnectedUserModel();
                TokenModel tokenModel = CommonMethods.GetTokenDataModel(Context.GetHttpContext());
                chatConnectedUserModel.ConnectionId = Context.ConnectionId;
                chatConnectedUserModel.UserId = userId;
                await _chatService.ChatConnectedUser(chatConnectedUserModel, tokenModel);
            }
            catch (Exception)
            {
                throw new HubException("Error in SignalR Hub Connect method!");
            }
        }

        /// <summary>
        /// he SendMessage method can be called by any connected client.
        /// It sends the received message to "all clients".
        /// SignalR code is asynchronous to provide maximum scalability.
        /// </summary>
        /// <param name="chatModel"></param>
        /// <returns></returns>
        public async Task<JsonModel> SendMessage(ChatModel chatModel)
        {
            JsonModel jsonModel = new JsonModel();
            TokenModel tokenModel = CommonMethods.GetTokenDataModel(Context.GetHttpContext());
            try
            {
                chatModel.ChatDate = DateTime.UtcNow;
                chatModel.IsSeen = false;
                chatModel.Id = chatModel.Id != null ? chatModel.Id : 0;
                jsonModel = await _chatService.SaveChat(chatModel, tokenModel);
                SaveNotification(chatModel, (int)NotificationActionType.ChatMessage, tokenModel);

                LocationModel locationModal = _locationService.GetLocationOffsets(tokenModel.LocationID, tokenModel);

                chatModel.UTCDateTimeForMobile = chatModel.ChatDate;
                chatModel.ChatDateForWebApp = CommonMethods.ConvertFromUtcTimeWithOffset((DateTime)chatModel.ChatDate, locationModal.DaylightOffset, locationModal.StandardOffset, locationModal.TimeZoneName, tokenModel);
                
                jsonModel.data = chatModel;

                if (chatModel.IsMobileUser)
                {
                    HC.Patient.Model.Common.NotificationModel notificationModel = _tokenService.GetLoginNotification(chatModel.ToUserId, tokenModel);
                    string connId = _chatService.GetConnectionId(chatModel.ToUserId);
                    if (connId != null)
                    {
                        await Clients.Client(connId).SendAsync("NotificationResponse", notificationModel);
                    }
                }
                else
                {
                    JsonModel notificationModel = _tokenService.GetChatAndNotificationCount(chatModel.ToUserId, tokenModel);
                    string connId = _chatService.GetConnectionId(chatModel.ToUserId);
                    if (connId != null)
                    {
                        await Clients.Client(connId).SendAsync("MobileNotificationResponse", notificationModel);
                    }
                }
                string connectionId = _chatService.GetConnectionId(chatModel.ToUserId);
                if (!string.IsNullOrEmpty(connectionId))
                    await Clients.Client(connectionId).SendAsync("ReceiveMessage", chatModel);
                return jsonModel;

                //else
                //    await Clients.All.SendAsync("ReceiveMessage", message, fromUserId);
            }
            catch (Exception ex)
            {
                tokenModel.Request = null;
                return new JsonModel(tokenModel, ex.ToString(), 500);
            }
        }

        /// <summary>
        /// this is use for message count notification by signalR for real time dynamic count for new message recieved
        /// </summary>
        /// <param name="forStaff"></param>
        /// <returns></returns>
        public async Task MessageCountRequest(bool forStaff)
        {
            HttpContext httpContext = Context.GetHttpContext();
            MessagesInfoFromSignalRModel messagesInfoFromSignalRModel = _messageService.ExecuteFunctions<MessagesInfoFromSignalRModel>(() => _messageService.GetMessagesInfoFromSignalR(forStaff, httpContext));
            await Clients.All.SendAsync("MessageCountResponse", messagesInfoFromSignalRModel);
        }

        public async Task NotificationRequest()
        {
            //HttpContext httpContext = Context.GetHttpContext();
            //TokenModel token = CommonMethods.GetTokenDataModel(httpContext);
            ////NotificationModel notificationModel = _tokenService.GetLoginNotification(token);
            //await Clients.All.SendAsync("NotificationResponse", notificationModel);
        }

        private static void ThrowIfInvalidOptions(JwtIssuerOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            if (options.ValidFor <= TimeSpan.Zero)
            {
                throw new ArgumentException("Must be a non-zero TimeSpan.", nameof(JwtIssuerOptions.ValidFor));
            }

            if (options.SigningCredentials == null)
            {
                throw new ArgumentNullException(nameof(JwtIssuerOptions.SigningCredentials));
            }

            if (options.JtiGenerator == null)
            {
                throw new ArgumentNullException(nameof(JwtIssuerOptions.JtiGenerator));
            }
        }

        private void SaveNotification(ChatModel chatModel, int actionId, TokenModel token)
        {
            NotificationModel notificationModel = new NotificationModel();
            if (chatModel.IsMobileUser) // from Patient
            {
                notificationModel.PatientId = _patientRepository.GetPatientByUserId(chatModel.FromUserId);
                Staffs staff = _tokenRepository.GetStaffByuserID(chatModel.ToUserId);
                notificationModel.StaffId = 0;
                if (staff != null)
                {
                    notificationModel.StaffId = staff.Id;
                }
            }
            else
            {
                notificationModel.PatientId = _patientRepository.GetPatientByUserId(chatModel.ToUserId);
                Staffs staff = _tokenRepository.GetStaffByuserID(chatModel.FromUserId);
                notificationModel.StaffId = 0;
                if (staff != null)
                {
                    notificationModel.StaffId = staff.Id;
                }
            }
            notificationModel.ActionTypeId = actionId;
            notificationModel.ChatId = chatModel.Id;
            notificationModel.Message = CommonMethods.Encrypt(chatModel.Message);
            notificationModel.IsMobileUser = chatModel.IsMobileUser;
            if (notificationModel.PatientId > 0 && notificationModel.StaffId > 0)
            {
                _notificationService.SaveNotification(notificationModel, token);
            }
        }
    }
}
