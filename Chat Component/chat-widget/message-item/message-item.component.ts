import { Component, OnInit, Input, AfterViewInit, Output, EventEmitter } from '@angular/core';
import { DialogService } from 'src/app/shared/layout/dialog/dialog.service';
import { ChatService } from 'src/app/platform/modules/client-portal/chat/chat.service';
import { NotifierService } from 'angular-notifier';
import { MethodCall } from '@angular/compiler';

@Component({
  selector: 'app-message-item',
  templateUrl: './message-item.component.html',
  styleUrls: ['./message-item.component.css']
})
export class MessageItemComponent implements OnInit, AfterViewInit {
  
  @Input() value: any;
  isChecked: boolean;
  link: any;

  constructor(private chatService: ChatService,
    private dialogService: DialogService,
    private notifier: NotifierService) {
     
  }

  ngOnInit() {
    this.link = window.location.origin + '/web/member-portal/scheduling';
    this.isChecked = false;
  }

  ngAfterViewInit(): void {
  }

  onConfirm(chatId: number, confirmtype: string) {
 
        this.chatService.UpdateChatForPOC(chatId, confirmtype).subscribe(
          response => {
            if (response.statusCode == 200) {
              this.notifier.notify('success', response.message);
         
              this.isChecked = true;
            }
            else {
              this.notifier.notify('error', response.message)
            }
          })
  }

}
