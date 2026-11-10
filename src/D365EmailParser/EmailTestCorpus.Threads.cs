using System.Collections.Generic;

internal static partial class EmailTestCorpus
{
    private static IEnumerable<EmailCase> Threads() => [.. ClientThreads(), .. LanguageThreads(), .. MixedThreads()];

    private const string OwaHistory = """
        <div id="appendonsend"></div>
        <hr style="display:inline-block;width:98%" tabindex="-1">
        <div id="divRplyFwdMsg" dir="ltr"><font face="Calibri, sans-serif" color="#000000" style="font-size:11pt"><b>From:</b> Bank Desk &lt;desk@bank.example&gt;<br><b>Sent:</b> Monday, November 9, 2026 9:00 AM<br><b>To:</b> Dana<br><b>Subject:</b> Transfers</font><div>&nbsp;</div></div>
        <div dir="ltr">Please send the list of transfers for today.</div>
        """;

    private static IEnumerable<EmailCase> ClientThreads() =>
    [
        Thread("Outlook divRplyFwdMsg",
            """
            <html><body>
            <div dir="ltr"><p>Thanks, that works for me. See you Monday.</p><p>Regards,<br>Alice</p></div>
            <div id="divRplyFwdMsg" dir="ltr"><hr>
              <b>From:</b> Bob &lt;bob@contoso.com&gt;<br><b>Sent:</b> Friday, September 5, 2026 4:12 PM<br>
              <b>To:</b> Alice &lt;alice@contoso.com&gt;<br><b>Subject:</b> Re: Project sync
              <div><p>Can we meet Monday?</p></div>
            </div>
            </body></html>
            """,
            keep: ["See you Monday"], drop: ["Can we meet Monday"]),

        Thread("D365 web editor <hr> + From:/Sent: header",
            """
            <html><body>
            <div><p>Approved. Please proceed with the order.</p></div>
            <div><hr />
              <p><b>From:</b> Supplier &lt;sales@vendor.com&gt;<br><b>Sent:</b> 04/09/2026 09:30<br><b>To:</b> Buyer<br><b>Subject:</b> Quote #4821</p>
              <p>Please confirm the attached quote.</p>
            </div>
            </body></html>
            """,
            keep: ["Approved"], drop: ["confirm the attached quote"]),

        Thread("Gmail gmail_quote",
            """
            <html><body>
            <div dir="ltr">Got it - shipping today.</div>
            <div class="gmail_quote">
              <div dir="ltr" class="gmail_attr">On Fri, Sep 5, 2026 at 2:00 PM Sam &lt;sam@gmail.com&gt; wrote:</div>
              <blockquote class="gmail_quote">When will this ship?</blockquote>
            </div>
            </body></html>
            """,
            keep: ["shipping today"], drop: ["When will this ship"]),

        Thread("-----Original Message----- separator",
            """
            <html><body>
            <div><p>No changes needed on our side.</p></div>
            <div>-----Original Message-----<br>From: Carol<br>Sent: yesterday<br>Subject: Review<br>Please review the doc.</div>
            </body></html>
            """,
            keep: ["No changes needed"], drop: ["Please review the doc"]),

        Thread("Bare From:/Sent: header paragraph",
            """
            <html><body>
            <p>Confirmed for 10am.</p>
            <p>From: Dave &lt;dave@x.com&gt; Sent: Monday To: Me Subject: Slot</p>
            <p>Are you free at 10am?</p>
            </body></html>
            """,
            keep: ["Confirmed for 10am"], drop: ["Are you free at 10am"]),

        Thread("Underscore rule + From:/Sent: inside WordSection1 wrapper",
            """
            <html><body><div class=WordSection1>
            <p class=MsoNormal>Payment released this morning.</p>
            <p class=MsoNormal>________________________________</p>
            <p class=MsoNormal><b>From:</b> Bob<br><b>Sent:</b> Monday<br><b>Subject:</b> Payment</p>
            <p class=MsoNormal>Please release the payment.</p>
            </div></body></html>
            """,
            keep: ["Payment released this morning"], drop: ["Please release the payment"]),

        Thread("Hebrew מאת:/נשלח: header after underscore rule",
            """
            <html><body dir=RTL><div class=WordSection1>
            <p class=MsoNormal dir=RTL>ההעברה בוצעה, תודה.</p>
            <div><p class=MsoNormal>________________________________</p>
            <p class=MsoNormal dir=RTL><b>מאת:</b> דני<br><b>נשלח:</b> יום שני<br><b>נושא:</b> העברה</p>
            <p class=MsoNormal dir=RTL>נא לבצע את ההעברה.</p></div>
            </div></body></html>
            """,
            keep: ["ההעברה בוצעה"], drop: ["נא לבצע את ההעברה"]),

        Thread("Generic <blockquote type=cite>",
            """
            <html><body>
            <div>Sounds good, ship it.</div>
            <div>On Mon, Sep 1, 2026, at 10:00, Eve &lt;eve@x.com&gt; wrote:</div>
            <blockquote type="cite"><div>Shall we ship on Tuesday?</div></blockquote>
            </body></html>
            """,
            keep: ["ship it"], drop: ["Shall we ship on Tuesday"]),

        Thread("Plain <blockquote> after '... wrote:' attribution",
            """
            <html><body>
            <p>Done, closing the ticket.</p>
            <p>On Tue, Sep 2, 2026 Frank wrote:</p>
            <blockquote><p>Can you close the ticket?</p></blockquote>
            </body></html>
            """,
            keep: ["closing the ticket"], drop: ["Can you close the ticket"]),

        Thread("New Outlook / Outlook on the web (appendonsend + hr + divRplyFwdMsg)",
            """
            <html><head><style type="text/css" style="display:none">P {margin-top:0;margin-bottom:0;}</style></head>
            <body dir="ltr">
            <div class="elementToProof" style="font-family: Aptos, Calibri, sans-serif; font-size: 12pt;">Invoice 4471 is approved for payment.</div>
            <div class="elementToProof" style="font-family: Aptos, Calibri, sans-serif; font-size: 12pt;">Regards, Maya</div>
            <div id="appendonsend"></div>
            <hr style="display:inline-block;width:98%" tabindex="-1">
            <div id="divRplyFwdMsg" dir="ltr"><font face="Calibri, sans-serif" style="font-size:11pt" color="#000000"><b>From:</b> Accounts &lt;ap@fabrikam.example&gt;<br><b>Sent:</b> Monday, November 9, 2026 10:12<br><b>To:</b> Maya &lt;maya@contoso.example&gt;<br><b>Subject:</b> Invoice 4471</font><div>&nbsp;</div></div>
            <div><div dir="ltr">Please approve invoice 4471 by Friday.</div></div>
            </body></html>
            """,
            keep: ["Invoice 4471 is approved", "Regards, Maya"], drop: ["approve invoice 4471 by Friday", "ap@fabrikam.example"]),

        Thread("Outlook received via D365 (x_ prefixed ids)",
            """
            <div class="x_elementToProof">Received, thank you.</div>
            <div id="x_appendonsend"></div>
            <hr style="display:inline-block; width:98%">
            <div id="x_divRplyFwdMsg" dir="ltr"><font face="Calibri, sans-serif" color="#000000" style="font-size:11pt"><b>From:</b> Lior &lt;lior@contoso.example&gt;<br><b>Sent:</b> Sunday, November 8, 2026 9:00 AM<br><b>To:</b> Support<br><b>Subject:</b> Docs</font><div>&nbsp;</div></div>
            <div class="x_elementToProof">Attached are the signed documents.</div>
            """,
            keep: ["Received, thank you"], drop: ["Attached are the signed documents"]),

        Thread("Outlook mobile (iOS/Android)",
            """
            <div dir="auto">On my way, will sign today.</div>
            <div id="ms-outlook-mobile-signature" dir="auto">Get <a href="https://aka.ms/o0ukef">Outlook for iOS</a></div>
            <hr style="display:inline-block;width:98%" tabindex="-1">
            <div id="divRplyFwdMsg" dir="ltr"><font face="Calibri, sans-serif" style="font-size:11pt" color="#000000"><b>From:</b> Noa &lt;noa@contoso.example&gt;<br><b>Sent:</b> Tuesday, November 10, 2026 8:01:22 AM<br><b>To:</b> Dan<br><b>Subject:</b> Contract</font><div>&nbsp;</div></div>
            <div dir="ltr">Can you sign the contract today?</div>
            """,
            keep: ["will sign today"], drop: ["Can you sign the contract"]),

        Thread("Outlook desktop (Word HTML, border-top header div)",
            """
            <html xmlns:o="urn:schemas-microsoft-com:office:office"><head><meta http-equiv="Content-Type" content="text/html; charset=utf-8"></head>
            <body lang="EN-US" link="#0563C1" vlink="#954F72"><div class="WordSection1">
            <p class="MsoNormal">Hi Ron,<o:p></o:p></p>
            <p class="MsoNormal">The wire was sent this morning.<o:p></o:p></p>
            <p class="MsoNormal"><o:p>&nbsp;</o:p></p>
            <div><div style="border:none;border-top:solid #E1E1E1 1.0pt;padding:3.0pt 0cm 0cm 0cm">
            <p class="MsoNormal"><b>From:</b> Ron Levi &lt;ron@fabrikam.example&gt; <br><b>Sent:</b> Monday, November 9, 2026 4:15 PM<br><b>To:</b> Treasury &lt;treasury@contoso.example&gt;<br><b>Subject:</b> Wire status<o:p></o:p></p>
            </div></div>
            <p class="MsoNormal"><o:p>&nbsp;</o:p></p>
            <p class="MsoNormal">Was the wire sent?<o:p></o:p></p>
            </div></body></html>
            """,
            keep: ["The wire was sent this morning"], drop: ["Was the wire sent?", "ron@fabrikam.example"]) with { KnownGap = true },

        Thread("Outlook desktop Hebrew (Word HTML, מאת:/נשלח:)",
            """
            <html><body lang="HE" dir="RTL"><div class="WordSection1" dir="rtl">
            <p class="MsoNormal" dir="RTL"><span lang="HE">ההעברה בוצעה היום בבוקר.</span></p>
            <p class="MsoNormal" dir="RTL"><o:p>&nbsp;</o:p></p>
            <div><div style="border:none;border-top:solid #E1E1E1 1.0pt;padding:3.0pt 0cm 0cm 0cm">
            <p class="MsoNormal" dir="RTL"><b><span lang="HE">מאת:</span></b><span lang="HE"> רון לוי &lt;ron@fabrikam.example&gt;<br><b>נשלח:</b> יום שני 09 נובמבר 2026 16:15<br><b>אל:</b> גזברות<br><b>נושא:</b> סטטוס העברה</span></p>
            </div></div>
            <p class="MsoNormal" dir="RTL"><span lang="HE">האם ההעברה בוצעה?</span></p>
            </div></body></html>
            """,
            keep: ["ההעברה בוצעה היום בבוקר"], drop: ["האם ההעברה בוצעה?"]) with { KnownGap = true },

        Thread("Outlook for Mac (From:/Date: header, no Sent:)",
            """
            <div class="WordSection1"><p class="MsoNormal">Confirmed, see you there.</p><p class="MsoNormal">&nbsp;</p>
            <div style="border:none;border-top:solid #B5C4DF 1.0pt;padding:3.0pt 0in 0in 0in"><p class="MsoNormal"><b><span style="color:black">From: </span></b><span style="color:black">Gal &lt;gal@example.com&gt;<br></span><b><span style="color:black">Date: </span></b><span style="color:black">Monday, 9 November 2026 at 10:00<br></span><b><span style="color:black">To: </span></b><span style="color:black">Team<br></span><b><span style="color:black">Subject: </span></b><span style="color:black">Offsite</span></p></div>
            <p class="MsoNormal">Are you joining the offsite?</p></div>
            """,
            keep: ["Confirmed, see you there"], drop: ["Are you joining the offsite?"]) with { KnownGap = true },

        Thread("OutlookMessageHeader class",
            """
            <div>Agreed, let's sign.</div>
            <div class="OutlookMessageHeader" lang="en-us" dir="ltr" align="left"><hr tabindex="-1"><font face="Tahoma" size="2"><b>From:</b> Legal<br><b>Sent:</b> Monday<br><b>Subject:</b> Terms<br></font><br></div>
            <div>Do you agree with the terms?</div>
            """,
            keep: ["Agreed, let's sign"], drop: ["Do you agree with the terms?"]),

        Thread("Gmail reply, Hebrew attribution",
            """
            <div dir="rtl">מאשר, תודה.</div><br>
            <div class="gmail_quote"><div dir="rtl" class="gmail_attr">&#8235;בתאריך יום ב׳, 9 בנוב׳ 2026 ב-10:00 מאת &#8234;רון&#8236;&rlm; &lt;<a href="mailto:ron@gmail.example">ron@gmail.example</a>&gt;:&#8236;<br></div>
            <blockquote class="gmail_quote" style="margin:0px 0px 0px 0.8ex;border-right:1px solid rgb(204,204,204);padding-right:1ex"><div dir="rtl">האם אתה מאשר את ההעברה?</div></blockquote></div>
            """,
            keep: ["מאשר, תודה"], drop: ["האם אתה מאשר את ההעברה?"]),

        Thread("Gmail forward nested inside the newest message's div",
            """
            <div dir="ltr">FYI - see the customer's request below.<br><br><div class="gmail_quote"><div dir="ltr" class="gmail_attr">---------- Forwarded message ---------<br>From: <strong class="gmail_sendername" dir="auto">Customer</strong> <span dir="auto">&lt;<a href="mailto:c@example.com">c@example.com</a>&gt;</span><br>Date: Mon, Nov 9, 2026 at 9:00 AM<br>Subject: Refund<br>To: &lt;support@contoso.example&gt;<br></div><br><br><div dir="ltr">I would like a refund for order 778.</div></div></div>
            """,
            keep: ["see the customer's request below"], drop: ["refund for order 778"]),

        Thread("Gmail three-level thread",
            """
            <div dir="ltr">Final: we go with vendor B.</div><br>
            <div class="gmail_quote"><div dir="ltr" class="gmail_attr">On Mon, Nov 9, 2026 at 11:00 AM Ann &lt;ann@example.com&gt; wrote:<br></div><blockquote class="gmail_quote" style="margin:0px 0px 0px 0.8ex"><div dir="ltr">Vendor B is cheaper.</div><br>
            <div class="gmail_quote"><div dir="ltr" class="gmail_attr">On Mon, Nov 9, 2026 at 10:00 AM Ben &lt;ben@example.com&gt; wrote:<br></div><blockquote class="gmail_quote" style="margin:0px 0px 0px 0.8ex"><div dir="ltr">Which vendor should we pick?</div></blockquote></div></blockquote></div>
            """,
            keep: ["we go with vendor B"], drop: ["Vendor B is cheaper", "Which vendor should we pick?"]),

        Thread("Apple Mail iOS (<blockquote type=cite>)",
            """
            <html><head><meta http-equiv="content-type" content="text/html; charset=utf-8"></head><body dir="auto"><div dir="ltr">Yes, Thursday works.</div><div dir="ltr"><br><div dir="ltr">Sent from my iPhone</div><blockquote type="cite">On Nov 9, 2026, at 18:20, Tal &lt;tal@icloud.example&gt; wrote:<br><br></blockquote></div><blockquote type="cite"><div dir="ltr">Can we move the meeting to Thursday?</div></blockquote></body></html>
            """,
            keep: ["Thursday works", "Sent from my iPhone"], drop: ["move the meeting to Thursday"]),

        Thread("Thunderbird (moz-cite-prefix + blockquote cite)",
            """
            <html><body><p>Shipment confirmed for Monday.</p><div class="moz-cite-prefix">On 09/11/2026 10:00, Omer wrote:<br></div><blockquote type="cite" cite="mid:abc@example.com"><p>Can you confirm the shipment date?</p></blockquote></body></html>
            """,
            keep: ["Shipment confirmed for Monday"], drop: ["confirm the shipment date"]),

        Thread("Yahoo classic (border-left blockquote after 'wrote:')",
            """
            <div><div>Payment received, thanks.</div></div><div class="yahoo_quoted"><div style="font-family:Helvetica"><div>On Monday, November 9, 2026, 10:00:00 AM GMT+2, Avi &lt;avi@yahoo.example&gt; wrote:</div><div><br></div><blockquote style="border-left:2px solid #1010FF;margin-left:5px;padding-left:5px"><div>Did you receive the payment?</div></blockquote></div></div>
            """,
            keep: ["Payment received, thanks"], drop: ["Did you receive the payment?"]),

        Thread("Yahoo modern (div.yahoo_quoted, no blockquote)",
            """
            <html><body><div class="ydp3f" style="font-family:Helvetica, Arial, sans-serif;font-size:13px;"><div dir="ltr">Invoice paid, thanks.</div></div><div id="ydp9yahoo_quoted_1" class="ydp9yahoo_quoted"><div style="font-family:'Helvetica Neue', Helvetica, Arial, sans-serif;font-size:13px;color:#26282a;"><div>On Monday, November 9, 2026 at 10:00:00 AM GMT+2, Avi &lt;avi@yahoo.example&gt; wrote:</div><div><br></div><div><br></div><div><div dir="ltr">Did you pay the invoice?</div></div></div></div></body></html>
            """,
            keep: ["Invoice paid, thanks"], drop: ["Did you pay the invoice?"]) with { KnownGap = true },

        Thread("ProtonMail (protonmail_quote)",
            """
            <div>Ticket closed.</div><div class="protonmail_quote">------- Original Message -------<br>On Monday, November 9th, 2026 at 10:00, Rina &lt;rina@proton.example&gt; wrote:<br><blockquote class="protonmail_quote" type="cite"><div>Please close the ticket.</div></blockquote></div>
            """,
            keep: ["Ticket closed."], drop: ["Please close the ticket."]),

        Thread("Freshdesk / helpdesk (freshdesk_quote)",
            """
            <div>We have issued the refund.</div><div class="freshdesk_quote"><blockquote class="freshdesk_quote">On Mon, 9 Nov at 10:00 AM , Customer &lt;c@example.com&gt; wrote:<br><div>Where is my refund?</div></blockquote></div>
            """,
            keep: ["We have issued the refund"], drop: ["Where is my refund?"]),

        Thread("Zimbra (hr#zwchr + From:/Sent:)",
            """
            <html><body><div style="font-family: arial"><div>Approved for Q4.</div><div><br></div><hr id="zwchr"><div style="color:#000;font-weight:normal;font-family:Helvetica,Arial,sans-serif;font-size:12pt;"><b>From: </b>"Budget" &lt;budget@example.com&gt;<br><b>To: </b>"Ops" &lt;ops@example.com&gt;<br><b>Sent: </b>Monday, November 9, 2026 10:00:00 AM<br><b>Subject: </b>Q4 budget<br></div><div><br></div><div>Please approve the Q4 budget.</div></div></body></html>
            """,
            keep: ["Approved for Q4"], drop: ["Please approve the Q4 budget"]),

        Thread("Outlook Express (blockquote + ----- Original Message -----)",
            """
            <BODY bgColor=#ffffff><DIV><FONT face=Arial size=2>Price list attached.</FONT></DIV>
            <BLOCKQUOTE style="PADDING-RIGHT: 0px; PADDING-LEFT: 5px; MARGIN-LEFT: 5px; BORDER-LEFT: #000000 2px solid; MARGIN-RIGHT: 0px">
            <DIV style="FONT: 10pt arial">----- Original Message ----- </DIV>
            <DIV style="BACKGROUND: #e4e4e4; FONT: 10pt arial"><B>From:</B> <A title=a@x.com href="mailto:a@x.com">Ami</A> </DIV>
            <DIV style="FONT: 10pt arial"><B>Sent:</B> Monday, November 09, 2026 10:00 AM</DIV>
            <DIV style="FONT: 10pt arial"><B>Subject:</B> Prices</DIV>
            <DIV><BR></DIV><DIV><FONT face=Arial size=2>Please send the price list.</FONT></DIV></BLOCKQUOTE></BODY>
            """,
            keep: ["Price list attached"], drop: ["Please send the price list"]),

        Thread("Samsung/Android (-------- Original message --------)",
            """
            <html><body><div dir="auto">Got it, thanks!</div><div dir="auto"><br></div><div dir="auto">Sent from my Galaxy</div><div><br></div><div align="left" dir="auto" style="font-size:100%;color:#000000"><div>-------- Original message --------</div><div>From: Yossi &lt;yossi@example.com&gt; </div><div>Date: 11/9/26 10:00 (GMT+02:00) </div><div>To: me@example.com </div><div>Subject: Keys </div><div><br></div></div>Did you get the keys?</body></html>
            """,
            keep: ["Got it, thanks!"], drop: ["Did you get the keys?"]),

        Thread("Text in one <div>: underscore rule + From:/Sent: lines",
            """
            <div>Thanks, received.<br><br>________________________________<br>From: Eli &lt;eli@example.com&gt;<br>Sent: Monday, November 9, 2026 10:00 AM<br>To: me<br>Subject: Docs<br><br>Did you receive the documents?</div>
            """,
            keep: ["Thanks, received."], drop: ["Did you receive the documents?"]),

        Thread("Text in one <div>: -----Original Message-----",
            """
            <div>Looks good to me.<br><br>-----Original Message-----<br>From: Rami &lt;rami@example.com&gt;<br>Sent: Monday, November 9, 2026 10:00 AM<br>Subject: Draft<br><br>Can you review the draft?</div>
            """,
            keep: ["Looks good to me."], drop: ["Can you review the draft?"]),

        Thread("Text in one <div>: ---------- Forwarded message ----------",
            """
            <div>FYI, see below.<br><br>---------- Forwarded message ----------<br>From: Customer &lt;c@example.com&gt;<br>Date: Mon, Nov 9, 2026<br>Subject: Refund<br><br>I want a refund for order 778.</div>
            """,
            keep: ["FYI, see below."], drop: ["refund for order 778"]),

        Thread("Plain-text body (no HTML): Outlook underscore rule",
            """
            Thanks, the documents are signed.

            ________________________________
            From: Eli eli@example.com
            Sent: Monday, November 9, 2026 10:00 AM
            To: Me
            Subject: Docs

            Please sign the documents.
            """,
            keep: ["the documents are signed"], drop: ["Please sign the documents."]) with { KnownGap = true },
    ];

    // Outlook header in the sender's UI language. Bare header paragraphs, then underscore-rule
    // (Outlook on the web / mobile plain-text style) headers in their own <div>.
    private static string HeaderParagraph(string newest, string header, string older)
        => $"<html><body><p>{newest}</p><p>{header}</p><p>{older}</p></body></html>";

    private static string RuleHeader(string newest, string header, string older, string dir = "ltr")
        => $"<div dir=\"{dir}\">{newest}</div><div dir=\"{dir}\">________________________________<br>{header}</div><div dir=\"{dir}\">{older}</div>";

    private static IEnumerable<EmailCase> LanguageThreads() =>
    [
        Thread("German header (Von:/Gesendet:)",
            HeaderParagraph("Danke, erledigt.", "Von: Max Muster &lt;max@example.de&gt; Gesendet: Montag, 9. November 2026 10:00 An: Team Betreff: Auftrag", "Bitte den Auftrag bestätigen."),
            keep: ["Danke, erledigt."], drop: ["Bitte den Auftrag"]),
        Thread("French header (De :/Envoyé :)",
            HeaderParagraph("Merci, c'est fait.", "De : Jean &lt;jean@example.fr&gt; Envoyé : lundi 9 novembre 2026 10:00 À : Équipe Objet : Commande", "Merci de confirmer la commande."),
            keep: ["Merci, c'est fait."], drop: ["confirmer la commande"]),
        Thread("Spanish header (De:/Enviado:)",
            HeaderParagraph("Gracias, hecho.", "De: Ana &lt;ana@example.es&gt; Enviado: lunes, 9 de noviembre de 2026 10:00 Para: Equipo Asunto: Pedido", "Por favor confirma el pedido."),
            keep: ["Gracias, hecho."], drop: ["confirma el pedido"]),
        Thread("Spanish header, current Outlook (De:/Enviado el:)",
            HeaderParagraph("Gracias, ya está.", "De: Ana &lt;ana@example.es&gt; Enviado el: lunes, 9 de noviembre de 2026 10:00 Para: Equipo Asunto: Factura", "Por favor revisa la factura."),
            keep: ["Gracias, ya está."], drop: ["revisa la factura"]),
        Thread("Italian header (Da:/Inviato:)",
            HeaderParagraph("Grazie, fatto.", "Da: Luca &lt;luca@example.it&gt; Inviato: lunedì 9 novembre 2026 10:00 A: Team Oggetto: Ordine", "Per favore conferma l'ordine."),
            keep: ["Grazie, fatto."], drop: ["conferma l'ordine"]),
        Thread("Dutch header (Van:/Verzonden:)",
            HeaderParagraph("Bedankt, geregeld.", "Van: Jan &lt;jan@example.nl&gt; Verzonden: maandag 9 november 2026 10:00 Aan: Team Onderwerp: Bestelling", "Graag de bestelling bevestigen."),
            keep: ["Bedankt, geregeld."], drop: ["bestelling bevestigen"]),
        Thread("Portuguese header (De:/Enviada:)",
            HeaderParagraph("Obrigado, feito.", "De: João &lt;joao@example.pt&gt; Enviada: segunda-feira, 9 de novembro de 2026 10:00 Para: Equipe Assunto: Pedido", "Por favor confirme o pedido."),
            keep: ["Obrigado, feito."], drop: ["confirme o pedido"]),
        Thread("Russian header (От:/Отправлено:)",
            HeaderParagraph("Спасибо, сделано.", "От: Иван &lt;ivan@example.ru&gt; Отправлено: понедельник, 9 ноября 2026 г. 10:00 Кому: Команда Тема: Заказ", "Пожалуйста, подтвердите заказ."),
            keep: ["Спасибо, сделано."], drop: ["подтвердите заказ"]),
        Thread("Arabic header after underscore rule (من:/تم الإرسال:)",
            RuleHeader("تم التحويل، شكراً.", "من: أحمد &lt;ahmad@example.com&gt;<br>تم الإرسال: الاثنين، 9 نوفمبر 2026 10:00<br>إلى: الفريق<br>الموضوع: تحويل", "هل تم التحويل؟", "rtl"),
            keep: ["تم التحويل، شكراً"], drop: ["هل تم التحويل؟"]),
        Thread("Polish header after underscore rule (Od:/Wysłane:)",
            RuleHeader("Przelew wykonany.", "Od: Jan &lt;jan@example.pl&gt;<br>Wysłane: poniedziałek, 9 listopada 2026 10:00<br>Do: Zespół<br>Temat: Przelew", "Czy przelew został wykonany?"),
            keep: ["Przelew wykonany."], drop: ["Czy przelew"]),
        Thread("Swedish header after underscore rule (Från:/Skickat:)",
            RuleHeader("Betalningen är klar.", "Från: Erik &lt;erik@example.se&gt;<br>Skickat: den 9 november 2026 10:00<br>Till: Team<br>Ämne: Betalning", "Är betalningen klar?"),
            keep: ["Betalningen är klar."], drop: ["Är betalningen klar?"]),
        Thread("Japanese header after underscore rule (差出人:/送信日時:)",
            RuleHeader("振込が完了しました。", "差出人: 山田 &lt;yamada@example.jp&gt;<br>送信日時: 2026年11月9日 10:00<br>宛先: チーム<br>件名: 振込", "振込は完了しましたか？"),
            keep: ["振込が完了しました"], drop: ["振込は完了しましたか"]),
        Thread("Chinese header after underscore rule (发件人:/发送时间:)",
            RuleHeader("付款已完成。", "发件人: 王伟 &lt;wang@example.cn&gt;<br>发送时间: 2026年11月9日 10:00<br>收件人: 团队<br>主题: 付款", "付款完成了吗？"),
            keep: ["付款已完成"], drop: ["付款完成了吗"]),
        Thread("Turkish header after underscore rule (Kimden:/Gönderildi:)",
            RuleHeader("Ödeme bugün yapıldı.", "Kimden: Ali &lt;ali@example.com.tr&gt;<br>Gönderildi: 9 Kasım 2026 Pazartesi 10:00<br>Kime: Ekip<br>Konu: Ödeme", "Ödeme yapıldı mı?"),
            keep: ["Ödeme bugün yapıldı."], drop: ["Ödeme yapıldı mı?"]),
        Thread("Hebrew -----הודעה מקורית----- separator",
            """<div dir="rtl"><p>קיבלתי, תודה.</p><p>-----הודעה מקורית-----</p><p>מאת: רון [mailto:ron@example.com]</p><p>נשלח: יום שני 9 נובמבר 2026 9:00</p><p>נושא: מסמכים</p><p>מצורפים המסמכים.</p></div>""",
            keep: ["קיבלתי, תודה."], drop: ["מצורפים המסמכים"]),
        Thread("Hebrew header with &rlm; marks after underscore rule",
            """<div dir="rtl">קיבלתי, אטפל היום.</div><div dir="rtl">________________________________<br>&rlm;מאת:&rlm; רון &lt;ron@example.com&gt;<br>&rlm;נשלח:&rlm; יום שני 9 נובמבר 2026 9:00<br>&rlm;נושא:&rlm; בקשה</div><div dir="rtl">אפשר לטפל בבקשה?</div>""",
            keep: ["קיבלתי, אטפל היום"], drop: ["אפשר לטפל בבקשה?"]),
        Thread("Plain <blockquote> after Polish attribution (napisał:)",
            """<p>Zgoda.</p><p>W dniu 9.11.2026 o 10:00 Jan &lt;jan@example.pl&gt; napisał:</p><blockquote><p>Czy się zgadzasz?</p></blockquote>""",
            keep: ["Zgoda."], drop: ["Czy się zgadzasz?"]),
        Thread("Plain <blockquote> after Hebrew Apple attribution (כתב/ה:)",
            """<div dir="rtl">מאושר.</div><div dir="rtl">ב-9 בנוב׳ 2026, בשעה 10:00, &rlm;דני &lt;dani@icloud.example&gt; כתב/ה:</div><blockquote><div dir="rtl">האם זה מאושר?</div></blockquote>""",
            keep: ["מאושר."], drop: ["האם זה מאושר?"]),
        Thread("Plain <blockquote> after Greek attribution with address (unlisted verb)",
            """<p>Εντάξει.</p><p>Στις 9/11/2026 10:00, ο Νίκος &lt;nikos@example.gr&gt; έγραψε:</p><blockquote><p>Είναι εντάξει;</p></blockquote>""",
            keep: ["Εντάξει."], drop: ["Είναι εντάξει;"]),
        Thread("Plain <blockquote> whose first line is '... wrote:'",
            """<div>Done, deployed.</div><blockquote><div>On Mon, Nov 9, 2026, Kim &lt;kim@example.com&gt; wrote:</div><div>Is it deployed?</div></blockquote>""",
            keep: ["Done, deployed."], drop: ["Is it deployed?"]),
        Thread("Plain <blockquote> with From:/Sent: header inside",
            """<p>See my answer.</p><blockquote style="border-left:2px solid #1010FF;margin-left:5px;padding-left:5px"><b>From: </b>"Ops" &lt;ops@example.com&gt;<br><b>Sent: </b>Monday, November 9, 2026<br><b>Subject: </b>Q<br><br>What is the answer?</blockquote>""",
            keep: ["See my answer."], drop: ["What is the answer?"]),
    ];

    private static IEnumerable<EmailCase> MixedThreads() =>
    [
        Thread("Reported request (----- lines) replied in Outlook on the web",
            TransferRequest(DashLine, OwaHistory),
            keep: TransferKeep, drop: ["list of transfers for today", "desk@bank.example"]),

        Thread("Reported request (_____ lines) replied in Outlook on the web",
            TransferRequest(UnderscoreLine, OwaHistory),
            keep: TransferKeep, drop: ["list of transfers for today", "desk@bank.example"]),

        Thread("Reported request (_____ lines) + Outlook underscore-rule header",
            TransferRequest(UnderscoreLine, """
                <p class="MsoNormal">________________________________</p>
                <p class="MsoNormal"><b>From:</b> Bank Desk &lt;desk@bank.example&gt;<br><b>Sent:</b> Monday, November 9, 2026 9:00 AM<br><b>To:</b> Dana<br><b>Subject:</b> Transfers</p>
                <p class="MsoNormal">Please send the list of transfers for today.</p>
                """),
            keep: TransferKeep, drop: ["list of transfers for today", "desk@bank.example"]),

        Thread("Reported request (<hr> lines) + D365 <hr> reply header",
            TransferRequest("<hr>", """
                <hr><p><b>From:</b> Bank Desk &lt;desk@bank.example&gt;<br><b>Sent:</b> Monday, November 9, 2026 9:00 AM<br><b>Subject:</b> Transfers</p>
                <p>Please send the list of transfers for today.</p>
                """),
            keep: TransferKeep, drop: ["list of transfers for today", "desk@bank.example"]),

        Thread("Gmail indent <blockquote> in newest + gmail_quote history",
            """<div dir="ltr"><div>Onboarding steps:</div><blockquote style="margin:0 0 0 40px;border:none;padding:0px"><div>1. Sign the NDA</div><div>2. Send a copy of your ID</div></blockquote><div>Thanks, HR</div><br><div class="gmail_quote"><div dir="ltr" class="gmail_attr">On Mon, Nov 9, 2026 at 9:00 AM Noam &lt;noam@example.com&gt; wrote:<br></div><blockquote class="gmail_quote" style="margin:0px 0px 0px 0.8ex;border-left:1px solid rgb(204,204,204);padding-left:1ex"><div dir="ltr">What do I need for onboarding?</div></blockquote></div></div>""",
            keep: ["2. Send a copy of your ID", "Thanks, HR"], drop: ["What do I need for onboarding?"]),

        Thread("Outlook web indent <blockquote> in newest + divRplyFwdMsg history",
            """<div class="elementToProof">Please prepare:</div><blockquote style="margin-top:0;margin-bottom:0"><div class="elementToProof">- the signed contract</div><div class="elementToProof">- the bank confirmation</div></blockquote><div class="elementToProof">Thanks, Yael</div><div id="appendonsend"></div><hr style="display:inline-block;width:98%" tabindex="-1"><div id="divRplyFwdMsg" dir="ltr"><font face="Calibri, sans-serif" color="#000000" style="font-size:11pt"><b>From:</b> Client &lt;client@example.com&gt;<br><b>Sent:</b> Monday, November 9, 2026 9:00 AM<br><b>To:</b> Yael<br><b>Subject:</b> Documents</font><div>&nbsp;</div></div><div class="elementToProof">Which documents do you need?</div>""",
            keep: ["the bank confirmation", "Thanks, Yael"], drop: ["Which documents do you need?"]),

        Thread("Inline images: only the newest message's image is returned",
            """
            <div dir="ltr"><p>Here is the signed form.</p><img src="cid:form001@contoso" alt="form.png" data-attachment-id="bbbbbbbb-0000-0000-0000-000000000001"></div>
            <div id="divRplyFwdMsg" dir="ltr"><hr><b>From:</b> Camille &lt;camille@example.com&gt;<br><b>Sent:</b> Tuesday, September 8, 2026 9:35 AM<br><b>Subject:</b> Form
            <div dir="ltr"><p>Please sign the older form.</p><img src="cid:old999@contoso" alt="old.png" data-attachment-id="99999999-9999-9999-9999-999999999999"></div></div>
            """,
            keep: ["Here is the signed form"], drop: ["Please sign the older form", "99999999-9999"]) with { LatestAttachments = 1 },

        Thread("History contains ----- lines (Hebrew OWA reply)",
            """<div dir="rtl"><p>ההעברות אושרו ובוצעו.</p><p>בברכה, דנה</p></div><div id="appendonsend"></div><hr style="display:inline-block;width:98%"><div id="divRplyFwdMsg" dir="rtl"><font face="Calibri, sans-serif" color="#000000" style="font-size:11pt"><b>מאת:</b> רון &lt;ron@example.com&gt;<br><b>נשלח:</b> יום שני 9 נובמבר 2026 9:00<br><b>אל:</b> דנה<br><b>נושא:</b> העברות</font><div>&nbsp;</div></div><div dir="rtl"><p>העברה ראשונה למר ישראל ישראלי</p><p>-----------------------------</p><p>העברה שנייה למר משה כהן</p></div>""",
            keep: ["ההעברות אושרו ובוצעו", "בברכה, דנה"], drop: ["העברה ראשונה", "העברה שנייה"]),

        Thread("D365 web editor Hebrew reply (<hr> + מאת:/נשלח:)",
            """<div dir="rtl"><p>אושר, ניתן להתקדם.</p></div><div><hr /><p dir="rtl"><b>מאת:</b> ספק &lt;sales@vendor.example&gt;<br><b>נשלח:</b> 09/11/2026 09:30<br><b>אל:</b> רכש<br><b>נושא:</b> הצעת מחיר 4821</p><p dir="rtl">נא לאשר את הצעת המחיר המצורפת.</p></div>""",
            keep: ["אושר, ניתן להתקדם"], drop: ["נא לאשר את הצעת המחיר"]),

        Thread("D365 reply with <strong> labels and &nbsp;",
            """<p>Order confirmed.</p><hr><p><strong>From:</strong>&nbsp;Supplier&nbsp;&lt;sales@vendor.example&gt;<br><strong>Sent:</strong>&nbsp;Monday, November 9, 2026 9:30 AM<br><strong>To:</strong>&nbsp;Buyer<br><strong>Subject:</strong>&nbsp;Order 99</p><p>Can you confirm order 99?</p>""",
            keep: ["Order confirmed."], drop: ["Can you confirm order 99?"]),

        Thread("Bare header paragraph inside a wrapper <div>",
            """<div><p>Confirmed for 10am.</p><p>From: Dave &lt;dave@example.com&gt; Sent: Monday To: Me Subject: Slot</p><p>Are you free at 10am?</p></div>""",
            keep: ["Confirmed for 10am"], drop: ["Are you free at 10am?"]),

        Thread("-----Original Message----- inside WordSection1 wrapper",
            """<div class="WordSection1"><p class="MsoNormal">No changes needed on our side.</p><p class="MsoNormal">-----Original Message-----<br>From: Carol &lt;carol@example.com&gt;<br>Sent: Monday, November 9, 2026 9:00 AM<br>Subject: Review</p><p class="MsoNormal">Please review the document.</p></div>""",
            keep: ["No changes needed on our side"], drop: ["Please review the document"]),

        Thread("Gmail reply whose newest message has a _____ signature line",
            """<div dir="ltr"><div>Report attached.</div><div>__________________</div><div>Noa Levi | Analyst</div></div><br><div class="gmail_quote"><div dir="ltr" class="gmail_attr">On Mon, Nov 9, 2026 at 9:00 AM Boss &lt;boss@example.com&gt; wrote:<br></div><blockquote class="gmail_quote" style="margin:0px 0px 0px 0.8ex"><div dir="ltr">Where is the report?</div></blockquote></div>""",
            keep: ["Report attached.", "Noa Levi | Analyst"], drop: ["Where is the report?"]),

        Thread("Newest message with signature _____ line + Outlook underscore-rule header",
            """<div><p>Contract signed and attached.</p><p>____________________</p><p>Dana Cohen | Legal</p></div><div><p>________________________________</p><p><b>From:</b> Client &lt;client@example.com&gt;<br><b>Sent:</b> Monday, November 9, 2026 9:00 AM<br><b>Subject:</b> Contract</p><p>Please sign the contract.</p></div>""",
            keep: ["Contract signed and attached", "Dana Cohen | Legal"], drop: ["Please sign the contract."]),
    ];
}
