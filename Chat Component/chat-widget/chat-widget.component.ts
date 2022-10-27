import { Component, OnInit, Input, Output, EventEmitter, Inject, ViewEncapsulation, ViewChild, OnChanges, ElementRef, Renderer2 } from '@angular/core';
import { MatDialogRef, MAT_DIALOG_DATA, MatButton } from '@angular/material';
import { CommonService } from '../../platform/modules/core/services';
import { ScrollbarComponent, } from 'ngx-scrollbar';
import { format } from 'date-fns';
import { ChatHistoryModel } from './chat-history.model';
import { SharedService } from '../shared.service';
//import * as moment from 'moment';
import * as moment from 'moment-timezone';
import { HubConnectionService } from 'src/app/hubconnection.service';

@Component({
  selector: 'app-chat-widget',
  templateUrl: './chat-widget.component.html',
  styleUrls: ['./chat-widget.component.scss'],
  encapsulation: ViewEncapsulation.None
})
export class ChatWidgetComponent implements OnInit, OnChanges {
  @Input() fromUserId: number;
  @Input() toUserId: number;
  @Input() allMessageArray: Array<ChatHistoryModel>;
  @Input() meta: any;
  @Input() imgSource: string = '';
  @Input() badge: number;
  @Input() title: string = '';
  @Input() subTitle: string = '';
  @Input() showCloseButton: boolean = false;
  @Input() autoFocus: boolean = true;
  @Input() isRoleClient: boolean = false;
  @Input() masterStaffs: Array<any> = []
  @Output() onLoadEarlier = new EventEmitter();
  @Output() onReceiveNotification = new EventEmitter();
  @Output() onCareManagerSelection = new EventEmitter();
  message: string;
  @ViewChild("scrollbar") scrollbarRef: ScrollbarComponent;
  @ViewChild("draggableChat") chatModal: ElementRef;
  previousScrollPosition: number;
  // updatedScrollPosition: number;
  careManagerId: number = null

  showChatModal: boolean;
  loadingEarlierMsg: boolean = false;
  pageNo: number = 1;
  pageSize: number = 10;
  isConnected: boolean;
  isDragging: boolean;
  isCMSelected: boolean = false
  currentLocationtimeZoneName: string;
  loginClientId: number;

  constructor(
    private sharedService: SharedService,
    private renderer: Renderer2,
    private _hubConnection: HubConnectionService,
    private commonService: CommonService
  ) {
    this.isRoleClient = false
    this.showChatModal = false;
    this.message = '';
    this.masterStaffs = this.masterStaffs.length > 0 ? this.masterStaffs : []
  }

  ngOnChanges() {
    if (this.previousScrollPosition) {
      this.loadingEarlierMsg = false;
      this.scrollbarRef.scrollYTo(this.previousScrollPosition);
      // this.scrollbarRef && this.scrollbarRef.scrollToBottom()
    }
    // if (this.updatedScrollPosition) {
    //   this.loadingEarlierMsg = false;
    //   this.scrollbarRef.scrollXTo(this.updatedScrollPosition);
    // }
  }
  compareDate(date, dateIndex) {
    let datesArray = this.allMessageArray.map((obj, index) => obj.chatDateForWebApp.split('T')[0]);
    return new Date(date) < new Date() && datesArray.indexOf(date.split('T')[0]) === dateIndex ? true : false;
  }
  onToggleChatModal() {
    this.showChatModal = !this.showChatModal;

    if (this.showChatModal) {
      setTimeout(() => {
        this.scrollbarRef && this.scrollbarRef.scrollToBottom();
        this.scrollbarRef.update();
      }, 1000);
    }
    // scrollable.scrollTo({top: 700})
    if (!this.isConnected && this._hubConnection.isConnected()) {
      this.isConnected = true;
      this.getMessageNotifications();
      this.getMessageNotifications1();
    } else {
      this.ReconnectOnClose(this.fromUserId);
    }

  }
  onCMSelect(event: any) {
    let CMName = event.source.triggerValue
    this.title = CMName
    let CMId = event.value
    this.careManagerId = CMId
    if (CMId > 0) { this.isCMSelected = true }
    this.onCareManagerSelection.emit({ CMId: CMId, CMName: CMName })
    setTimeout(() => {
      this.scrollbarRef && this.scrollbarRef.scrollToBottom();
      this.scrollbarRef.update();
    }, 1000);
  }

  loadEarlierMessages() {
    if (this.pageNo < (this.meta && this.meta.totalPages)) {
      this.pageNo = this.pageNo + 1;
      this.loadingEarlierMsg = true;
      this.onLoadEarlier.emit({ pageNo: this.pageNo });
      this.previousScrollPosition = this.scrollbarRef.view.offsetWidth;
      // this.scrollbarRef && this.scrollbarRef.scrollToBottom()
    }
  }

  ngOnInit() {
    if (this.isRoleClient != true)
      this.isCMSelected = true
    this.commonService.chatNavigationData.subscribe(({ isOpenChat, careManagerId }) => {
      if (isOpenChat) {
        this.showChatModal = true;
        this.careManagerId = careManagerId > 0 ? careManagerId : null
        if (this.careManagerId != null && this.isRoleClient) {
          this.isCMSelected = true
          this.onCareManagerSelection.emit({ CMId: this.careManagerId, CMName: '' })
        }
        setTimeout(() => {
          this.scrollbarRef && this.scrollbarRef.scrollToBottom();
          this.scrollbarRef.update();
        }, 1000);
      }
    });

  }

  appendNewMessage(msgObj: ChatHistoryModel, isRecieved: boolean = true) {
    //const messageObj: ChatHistoryModel = {
    //  id: msgObj.id,
    //  // chatDate: moment.utc(msgObj.chatDate).local().format('YYYY-MM-DDTHH:mm:ss'),
    //  chatDateForWebApp: format(msgObj.chatDateForWebApp, 'YYYY-MM-DDTHH:mm:ss'),
    //  message: msgObj.message,
    //  isRecieved: isRecieved,
    //}
     msgObj.chatDateForWebApp = format(msgObj.chatDateForWebApp.slice(0, -1), 'YYYY-MM-DDTHH:mm:ss');

    this.allMessageArray.push(msgObj);
    this.scrollbarRef && this.scrollbarRef.scrollToBottom(200);
  }

  sendMessage(event: any, input: any) {
    if (!this.message || !this.message.trim()) {
      input.focus();
      return false;
    }
    const chatDate = format(new Date(), 'YYYY-MM-DDTHH:mm:ss');
    this.handleNewUserMessage(this.message, chatDate);
    this.message = '';
    input.focus();
  }
  handleNewUserMessage(message: string = '', chatDate: string) {
    const chatModel = {
      id: null,
      fromUserId: this.fromUserId,
      toUserId: this.toUserId,
      message,
      chatDate: chatDate,
      isMobileUser: this.isRoleClient ? true : false,
    }
    if (this._hubConnection.isConnected()) {
      this._hubConnection.getHubConnection()
        .invoke('SendMessage', chatModel).then((res) => {
          if (res && res.data) {
            this.appendNewMessage(res.data, false);
          }
        })
        .catch((err) => console.error(err, 'ReceiveMessageReceiveMessageerror'));
      return message;
    } else {
      this._hubConnection.restartHubConnection().then(() => {
        this._hubConnection.getHubConnection()
          .invoke('SendMessage', chatModel).then((res) => {
            if (res && res.data) {
              this.appendNewMessage(res.data, false);
            }
          })
          .catch((err) => console.error(err, 'ReceiveMessageReceiveMessageerror'));
        return message;
      });
    }
  }
  getMessageNotifications() {
    this._hubConnection.getHubConnection().on('ReceiveMessage', (result) => {
      if (result.fromUserId == this.toUserId) {
        this.appendNewMessage(result, true);
      }
    });
  }
  getMessageNotifications1() {
    this._hubConnection.getHubConnection().on('NotificationResponse', (result) => {
      //console.log("NotificationResponse", result)
      this.onReceiveNotification.emit(result);
    });
  }

  ReconnectOnClose(fromUserId) {
    setTimeout(() => {
      this._hubConnection.restartHubConnection().then(() => {
      });
    }, 5000);
  }

  onDragChat(event: DragEvent) {
    this.isDragging = false;

    let bottom = this.chatModal.nativeElement.style.bottom,
      right = this.chatModal.nativeElement.style.right;

    bottom = bottom.replace('px', ''), right = right.replace('px', '');
    bottom = (parseInt(bottom) || 0) + (-event.y), right = (parseInt(right) || 0) + (-event.x);
    this.renderer.setStyle(this.chatModal.nativeElement, 'bottom', `${bottom}px`)
    this.renderer.setStyle(this.chatModal.nativeElement, 'right', `${right}px`)

  }

  onDragging() {
    this.isDragging = true;
  }
}
