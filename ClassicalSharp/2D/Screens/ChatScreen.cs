﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using OpenTK.Input;

namespace ClassicalSharp {
	
	public class ChatScreen : Screen {
		
		public ChatScreen( Game game ) : base( game ) {
			chatLines = game.ChatLines;
		}
		
		int chatLines;
		ChatTextWidget announcement, clock;
		TextInputWidget textInput;
		TextGroupWidget status, bottomRight, normalChat, clientStatus;
		bool suppressNextPress = true;
		int chatIndex;
		int blockSize;
		
		Font chatFont, chatBoldFont, chatItalicFont, chatUnderlineFont, announcementFont;
		public override void Init() {
			int fontSize = (int)(12 * game.GuiChatScale);
			Utils.Clamp( ref fontSize, 8, 60 );
			chatFont = new Font( game.FontName, fontSize );
			chatBoldFont = new Font( game.FontName, fontSize, FontStyle.Bold );
			chatItalicFont = new Font( game.FontName, fontSize, FontStyle.Italic );
			chatUnderlineFont = new Font( game.FontName, fontSize, FontStyle.Underline );
			
			fontSize = (int)(14 * game.GuiChatScale);
			Utils.Clamp( ref fontSize, 8, 60 );
			announcementFont = new Font( game.FontName, fontSize );
			blockSize = (int)(23 * 2 * game.GuiHotbarScale);
			ConstructWidgets();
			
			int[] indices = new int[chatLines];
			for( int i = 0; i < indices.Length; i++ )
				indices[i] = -1;
			Metadata = indices;
			SetInitialMessages();
			
			game.Events.ChatReceived += ChatReceived;
			game.Events.ChatFontChanged += ChatFontChanged;
			game.Events.ColourCodesChanged += ColourCodesChanged;
		}
		
		void ConstructWidgets() {
			textInput = new TextInputWidget( game, chatFont, chatBoldFont );
			textInput.YOffset = blockSize + 5;
			status = new TextGroupWidget( game, 4, chatFont, chatUnderlineFont, 
			                             Anchor.BottomOrRight, Anchor.LeftOrTop );
			status.Init();
			bottomRight = new TextGroupWidget( game, 3, chatFont, chatUnderlineFont, 
			                                  Anchor.BottomOrRight, Anchor.BottomOrRight );
			bottomRight.YOffset = blockSize * 3 / 2;
			bottomRight.Init();
			normalChat = new TextGroupWidget( game, chatLines, chatFont, chatUnderlineFont, 
			                                 Anchor.LeftOrTop, Anchor.BottomOrRight );
			normalChat.XOffset = 10;
			normalChat.YOffset = blockSize * 2 + 15;
			normalChat.Init();
			clientStatus = new TextGroupWidget( game, game.Chat.ClientStatus.Length, chatFont, 
			                                   chatUnderlineFont, Anchor.LeftOrTop, Anchor.BottomOrRight );
			clientStatus.XOffset = 10;
			clientStatus.YOffset = blockSize * 2 + 15;
			clientStatus.Init();
			announcement = ChatTextWidget.Create( game, 0, 0, null, 
			                                     Anchor.Centre, Anchor.Centre, announcementFont );
			announcement.YOffset = -game.Height / 4;
			clock = ChatTextWidget.Create( game, 0, 0, null, 
			                              Anchor.BottomOrRight, Anchor.LeftOrTop, chatItalicFont );
		}
		
		void SetInitialMessages() {
			Chat chat = game.Chat;
			chatIndex = chat.Log.Count - chatLines;
			ResetChat();
			status.SetText( 0, chat.Status1.Text );
			status.SetText( 1, chat.Status2.Text );
			status.SetText( 2, chat.Status3.Text );
			if( game.ShowClock ) clock.SetText( chat.ClientClock.Text );
			
			bottomRight.SetText( 2, chat.BottomRight1.Text );
			bottomRight.SetText( 1, chat.BottomRight2.Text );
			bottomRight.SetText( 0, chat.BottomRight3.Text );
			announcement.SetText( chat.Announcement.Text );
			for( int i = 0; i < chat.ClientStatus.Length; i++ )
				clientStatus.SetText( i, chat.ClientStatus[i].Text );
			
			if( game.chatInInputBuffer != null ) {
				OpenTextInputBar( game.chatInInputBuffer );
				game.chatInInputBuffer = null;
			}
		}
		
		public override void Render( double delta ) {
			if( !game.PureClassicMode ) {
				status.Render( delta );
				bottomRight.Render( delta );
			}
			
			if( game.ShowClock ) {
				if( !clock.IsValid ) clock.SetText( game.Chat.ClientClock.Text );
				clock.Render( delta );
			} else if( clock.IsValid ) {
				clock.Dispose();
			}
			int statusOffset = clock.IsValid ? clock.Height : 0;
			if( statusOffset != oldStatusOffset ) {
				oldStatusOffset = statusOffset;
				status.MoveTo( status.X, oldStatusOffset );
			}
			
			UpdateChatYOffset( false );
			RenderClientStatus();
			DateTime now = DateTime.UtcNow;
			if( HandlesAllInput )
				normalChat.Render( delta );
			else
				RenderRecentChat( now, delta );
			
			if( !game.PureClassicMode )
				announcement.Render( delta );
			if( HandlesAllInput )
				textInput.Render( delta );
			
			if( announcement.IsValid && (now - game.Chat.Announcement.Received).TotalSeconds > 5 )
				announcement.Dispose();
		}
		
		void RenderRecentChat( DateTime now, double delta ) {
			int[] metadata = (int[])Metadata;
			for( int i = 0; i < normalChat.Textures.Length; i++ ) {
				Texture texture = normalChat.Textures[i];
				if( !texture.IsValid ) continue;
				
				DateTime received = game.Chat.Log[metadata[i]].Received;
				if( (now - received).TotalSeconds <= 10 )
					texture.Render( graphicsApi );
			}
		}
		
		void RenderClientStatus() {
			int y = clientStatus.Y + clientStatus.Height;
			for( int i = 0; i < clientStatus.Textures.Length; i++ ) {
				Texture texture = clientStatus.Textures[i];
				if( !texture.IsValid ) continue;
				
				y -= texture.Height;
				texture.Y1 = y;
				texture.Render( graphicsApi );
			}
		}
		
		static FastColour backColour = new FastColour( 60, 60, 60, 180 );
		public void RenderBackground() {
			int height = normalChat.GetUsedHeight();
			int y = normalChat.Y + normalChat.Height - height - 5;
			int x = normalChat.X - 5;
			int width = Math.Max( clientStatus.Width, normalChat.Width ) + 10;
			
			int boxHeight = height + clientStatus.GetUsedHeight();
			if( boxHeight > 0 )
				graphicsApi.Draw2DQuad( x, y, width, boxHeight + 10, backColour );
		}
		
		int inputOldHeight = -1, oldStatusOffset = -1;
		void UpdateChatYOffset( bool force ) {
			int height = textInput.RealHeight;
			if( force || height != inputOldHeight ) {
				clientStatus.YOffset = height + blockSize + 15;
				int y = game.Height - clientStatus.Height - clientStatus.YOffset;
				clientStatus.MoveTo( clientStatus.X, y );
				
				normalChat.YOffset = clientStatus.YOffset + clientStatus.GetUsedHeight();
				y = game.Height - normalChat.Height - normalChat.YOffset;
				normalChat.MoveTo( normalChat.X, y );
				inputOldHeight = height;
			}
		}

		void ColourCodesChanged( object sender, EventArgs e ) {
			textInput.altText.UpdateColours();
			UpdateChatYOffset( true );
			textInput.MoveTo( textInput.X, textInput.Y );
		}

		void ChatReceived( object sender, ChatEventArgs e ) {
			MessageType type = e.Type;
			if( type == MessageType.Normal ) {
				chatIndex++;
				if( game.ChatLines == 0 ) return;
				List<ChatLine> chat = game.Chat.Log;
				normalChat.PushUpAndReplaceLast( chat[chatIndex + chatLines - 1].Text );
				
				int[] metadata = (int[])Metadata;
				for( int i = 0; i < chatLines - 1; i++ )
					metadata[i] = metadata[i + 1];
				metadata[chatLines - 1] = chatIndex + chatLines - 1;
			} else if( type >= MessageType.Status1 && type <= MessageType.Status3 ) {
				status.SetText( (int)(type - MessageType.Status1), e.Text );
			} else if( type >= MessageType.BottomRight1 && type <= MessageType.BottomRight3 ) {
				bottomRight.SetText( 2 - (int)(type - MessageType.BottomRight1), e.Text );
			} else if( type == MessageType.Announcement ) {
				announcement.SetText( e.Text );
			} else if( type >= MessageType.ClientStatus1 && type <= MessageType.ClientStatus6 ) {
				clientStatus.SetText( (int)(type - MessageType.ClientStatus1), e.Text );
				UpdateChatYOffset( true );
			} else if( type == MessageType.ClientClock && game.ShowClock ) {
				clock.SetText( e.Text );
			}
		}

		public override void Dispose() {
			if( HandlesAllInput ) {
				game.chatInInputBuffer = textInput.chatInputText.ToString();
				if( game.CursorVisible )
					game.CursorVisible = false;
			} else {
				game.chatInInputBuffer = null;
			}
			chatFont.Dispose();
			chatItalicFont.Dispose();
			chatBoldFont.Dispose();
			chatUnderlineFont.Dispose();
			announcementFont.Dispose();
			
			normalChat.Dispose();
			textInput.DisposeFully();
			status.Dispose();
			bottomRight.Dispose();
			clientStatus.Dispose();
			announcement.Dispose();
			
			game.Events.ChatReceived -= ChatReceived;
			game.Events.ChatFontChanged -= ChatFontChanged;
			game.Events.ColourCodesChanged -= ColourCodesChanged;
		}
		
		void ChatFontChanged( object sender, EventArgs e ) {
			if( !game.Drawer2D.UseBitmappedChat ) return;
			Dispose();
			Init();
			UpdateChatYOffset( true );
		}
		
		public override void OnResize( int oldWidth, int oldHeight, int width, int height ) {
			announcement.OnResize( oldWidth, oldHeight, width, height );
			announcement.YOffset = -height / 4;
			announcement.MoveTo( announcement.X, announcement.YOffset - announcement.Height / 2 );
			blockSize = (int)(23 * 2 * game.GuiHotbarScale);
			textInput.YOffset = blockSize + 5;
			bottomRight.YOffset = blockSize * 3 / 2;
			
			int inputY = game.Height - textInput.Height - textInput.YOffset;
			textInput.MoveTo( textInput.X, inputY );
			status.OnResize( oldWidth, oldHeight, width, height );
			bottomRight.OnResize( oldWidth, oldHeight, width, height );
			UpdateChatYOffset( true );
		}

		void ResetChat() {
			normalChat.Dispose();
			List<ChatLine> chat = game.Chat.Log;
			int[] metadata = (int[])Metadata;
			for( int i = 0; i < chatLines; i++ )
				metadata[i] = -1;
			
			for( int i = chatIndex; i < chatIndex + chatLines; i++ ) {
				if( i >= 0 && i < chat.Count ) {
					normalChat.PushUpAndReplaceLast( chat[i].Text );
					metadata[i - chatIndex] = i;
				}
			}
		}
		
		public override bool HandlesKeyPress( char key ) {
			if( !HandlesAllInput ) return false;
			if( suppressNextPress ) {
				suppressNextPress = false;
				return false;
			}
			return textInput.HandlesKeyPress( key );
		}
		
		public void OpenTextInputBar( string initialText ) {
			if( !game.CursorVisible )
				game.CursorVisible = true;
			suppressNextPress = true;
			HandlesAllInput = true;
			game.Keyboard.KeyRepeat = true;
			
			textInput.chatInputText.Clear();
			textInput.chatInputText.Append( 0, initialText );
			textInput.Init();
		}
		
		public void AppendTextToInput( string text ) {
			if( !HandlesAllInput ) return;
			textInput.AppendText( text );
		}
		
		public override bool HandlesKeyDown( Key key ) {
			suppressNextPress = false;

			if( HandlesAllInput ) { // text input bar
				if( key == game.Mapping( KeyBinding.SendChat ) || key == Key.KeypadEnter
				   || key == game.Mapping( KeyBinding.PauseOrExit ) ) {
					HandlesAllInput = false;
					if( game.CursorVisible )
						game.CursorVisible = false;
					game.Camera.RegrabMouse();
					game.Keyboard.KeyRepeat = false;
					
					if( key == game.Mapping( KeyBinding.PauseOrExit ) )
						textInput.Clear();
					textInput.SendTextInBufferAndReset();
					
					chatIndex = game.Chat.Log.Count - chatLines;
					ResetIndex();
				} else if( key == Key.PageUp ) {
					chatIndex -= chatLines;
					ResetIndex();
				} else if( key == Key.PageDown ) {
					chatIndex += chatLines;
					ResetIndex();
				} else {
					textInput.HandlesKeyDown( key );
				}
				return key < Key.F1 || key > Key.F35;
			}

			if( key == game.Mapping( KeyBinding.OpenChat ) ) {
				OpenTextInputBar( "" );
			} else if( key == Key.Slash ) {
				OpenTextInputBar( "/" );
			} else {
				return false;
			}
			return true;
		}
		
		public override bool HandlesMouseScroll( int delta ) {
			if( !HandlesAllInput ) return false;
			chatIndex -= delta;
			ResetIndex();
			return true;
		}
		
		public override bool HandlesMouseClick( int mouseX, int mouseY, MouseButton button ) {
			if( !HandlesAllInput || game.HideGui ) return false;
			if( normalChat.Bounds.Contains( mouseX, mouseY ) ) {
				int height = normalChat.GetUsedHeight();
				int y = normalChat.Y + normalChat.Height - height;
				if( new Rectangle( normalChat.X, y, normalChat.Width, height ).Contains( mouseX, mouseY ) ) {
					string text = normalChat.GetSelected( mouseX, mouseY );
					if( text == null ) return false;
					
					if( Utils.IsUrlPrefix( text ) ) {
						game.ShowWarning( new WarningScreen(
							game, text, false, "Are you sure you want to go to this url?",
							OpenUrl, AppendUrl, null, text,
							"Be careful - urls from strangers may link to websites that",
							" may have viruses, or things you may not want to open/see."
						) );
					} else if( game.ClickableChat ) {
						for( int i = 0; i < text.Length; i++ ) {
							if( !IsValidInputChar( text[i] ) ) {
								game.Chat.Add( "&eChatline contained characters that can't be sent on this server." );
								return true;
							}
						}
						textInput.AppendText( text );
					}
					return true;
				}
				return false;
			}
			return textInput.HandlesMouseClick( mouseX, mouseY, button );
		}
		
		void OpenUrl( WarningScreen screen ) {
			try {
				Process.Start( (string)screen.Metadata );
			} catch( Exception ex ) {
				ErrorHandler.LogError( "ChatScreen.OpenUrl", ex );
			}
		}
		
		void AppendUrl( WarningScreen screen ) {
			if( !game.ClickableChat ) return;
			textInput.AppendText( (string)screen.Metadata );
		}
		
		void ResetIndex() {
			int maxIndex = game.Chat.Log.Count - chatLines;
			int minIndex = Math.Min( 0, maxIndex );
			Utils.Clamp( ref chatIndex, minIndex, maxIndex );
			ResetChat();
		}
	}
}