# Matrice dei test – caso → test → esito

Esiti presi da [`test-report.txt`](test-report.txt) (generato da `build.ps1`). `n/m` = esecuzioni superate / totali
del metodo (i `Theory` contano un'esecuzione per caso). Prefissi: `CaseMatrixTests`, `ConditionTests`, `TokenTests`,
`ConflictTests`, `EngineAndUndoTests`, `ServicesTests`, `WindowsRecycleBinTests` → `tests/ExplorerOrganizer.Tests`;
`InProcessUiTests`, `EndToEndTests` → `tests/ExplorerOrganizer.UiTests`.

**Totale esecuzioni nel report: 211 · superate: 211 · fallite: 0**

## Condizioni

| Caso | Test | Esito |
|---|---|---|
| Nome: è, contiene, inizia, finisce, wildcard, regex | `ConditionTests.Name_operators`<br>`ConditionTests.Name_case_sensitive_is_respected` | ✅ 11/11 · ✅ 1/1 |
| Nome: bersaglio nome / nome+estensione / percorso / cartella | `CaseMatrixTests.Condition_name_targets` | ✅ 5/5 |
| Nome: operatore "è vuoto" | `CaseMatrixTests.Condition_name_is_empty_operator` | ✅ 1/1 |
| Regex: gruppi catturati e regex non valida | `ConditionTests.Regex_captures_are_available_for_tokens`<br>`ConditionTests.Invalid_regex_never_matches_and_is_reported` | ✅ 1/1 · ✅ 1/1 |
| Estensione (lista, punti, maiuscole) | `CaseMatrixTests.Condition_extension`<br>`ConditionTests.Extension_list_ignores_dots_case_and_spaces` | ✅ 1/1 · ✅ 1/1 |
| Dimensione (>, <, =, tra; B/KB/MB/GB) | `CaseMatrixTests.Condition_size_units_and_comparisons`<br>`ConditionTests.Size_in_kilobytes` | ✅ 5/5 · ✅ 4/4 |
| Data creazione / modifica / accesso (relativa e assoluta) | `CaseMatrixTests.Condition_date_fields`<br>`ConditionTests.Date_relative_and_absolute` | ✅ 3/3 · ✅ 1/1 |
| Data di scatto EXIF | `CaseMatrixTests.Condition_date_taken_exif`<br>`ConditionTests.Date_taken_uses_metadata_and_is_false_when_missing` | ✅ 1/1 · ✅ 1/1 |
| Tipo di file (8 categorie) | `CaseMatrixTests.Condition_file_type_each_category`<br>`ConditionTests.File_type_category` | ✅ 8/8 · ✅ 1/1 |
| Attributi (nascosto, sola lettura, sistema, archivio) | `CaseMatrixTests.Condition_attribute_each_kind`<br>`ConditionTests.Attribute_condition` | ✅ 4/4 · ✅ 1/1 |
| Metadati (EXIF / ID3 / documento / System.*) | `CaseMatrixTests.Condition_metadata`<br>`ConditionTests.Metadata_condition_with_alias_and_is_empty` | ✅ 1/1 · ✅ 1/1 |
| Contenuto testuale (txt, docx) | `CaseMatrixTests.Condition_content`<br>`ConditionTests.Content_condition`<br>`ServicesTests.Content_reader_reads_docx_text` | ✅ 1/1 · ✅ 1/1 · ✅ 1/1 |
| Tutti i file / gruppo vuoto | `CaseMatrixTests.Condition_all_files`<br>`ConditionTests.Empty_group_matches_nothing_but_all_files_matches_everything` | ✅ 1/1 · ✅ 1/1 |
| AND (tutte) | `CaseMatrixTests.Condition_group_and` | ✅ 1/1 |
| OR (almeno una) | `CaseMatrixTests.Condition_group_or` | ✅ 1/1 |
| NOT (condizione e gruppo) | `CaseMatrixTests.Condition_not_on_single_condition_and_on_group` | ✅ 1/1 |
| Gruppi annidati AND/OR/NOT | `ConditionTests.Groups_and_or_not_nested` | ✅ 1/1 |

## Azioni

| Caso | Test | Esito |
|---|---|---|
| Sposta | `CaseMatrixTests.Action_move` | ✅ 1/1 |
| Copia | `CaseMatrixTests.Action_copy` | ✅ 1/1 |
| Rinomina / cambia estensione | `CaseMatrixTests.Action_rename_and_change_extension` | ✅ 1/1 |
| Ordina in sottocartelle (data, tipo, estensione, iniziale) | `CaseMatrixTests.Action_sort_into_subfolders`<br>`EngineAndUndoTests.Sort_into_subfolders_by_type_extension_letter_and_date` | ✅ 1/1 · ✅ 1/1 |
| Elimina nel Cestino | `CaseMatrixTests.Action_delete_to_recycle_bin`<br>`EngineAndUndoTests.Delete_goes_to_recycle_bin_by_default_and_undo_restores` | ✅ 1/1 · ✅ 1/1 |
| Elimina definitivamente (con conferma; senza conferma → Cestino) | `CaseMatrixTests.Action_delete_permanent_requires_confirmation`<br>`EngineAndUndoTests.Permanent_delete_without_confirmation_falls_back_to_recycle_bin`<br>`EngineAndUndoTests.Permanent_delete_with_confirmation_is_asked_once_and_marked_irreversible` | ✅ 1/1 · ✅ 1/1 · ✅ 1/1 |
| Comprimi ZIP | `CaseMatrixTests.Action_compress_zip` | ✅ 1/1 |
| Estrai ZIP (+ protezione zip-slip) | `CaseMatrixTests.Action_extract_zip`<br>`EngineAndUndoTests.Extract_blocks_zip_slip` | ✅ 1/1 · ✅ 1/1 |
| Crea collegamento .lnk | `CaseMatrixTests.Action_create_shortcut` | ✅ 1/1 |
| Apri con programma (argomenti con token) | `CaseMatrixTests.Action_open_with_runs_program_with_rendered_arguments` | ✅ 1/1 |
| Più azioni in sequenza | `CaseMatrixTests.Action_chain_runs_in_order_on_the_same_file` | ✅ 1/1 |
| Rimuovi cartella di origine se vuota | `EngineAndUndoTests.Remove_empty_source_folder_never_removes_the_root` | ✅ 1/1 |
| Ordine regole: vince la prima, regole disattivate ignorate | `EngineAndUndoTests.First_matching_rule_wins_and_disabled_rules_are_ignored` | ✅ 1/1 |

## Token di rinomina

| Caso | Test | Esito |
|---|---|---|
| {name} {ext} {fullname} {parent} {date} {now} {time} {created} {modified} {accessed} {taken} {counter} {size} {bytes} {hash:md5|sha1|sha256}, filtri upper/lower/title/left/trim/default, graffe letterali | `CaseMatrixTests.Token_renders_in_file_names` | ✅ 27/27 |
| {category} (localizzato) | `CaseMatrixTests.Token_category`<br>`TokenTests.Category_is_localized` | ✅ 1/1 · ✅ 1/1 |
| {exif:…} {id3:…} {doc:…} {prop:…} | `CaseMatrixTests.Token_metadata`<br>`TokenTests.Metadata_tokens_exif_id3_doc_prop_and_default` | ✅ 4/4 · ✅ 1/1 |
| {match:N} / {match:nome} | `CaseMatrixTests.Token_match_from_regex_condition` | ✅ 1/1 |
| {env:VAR} e %VAR% | `CaseMatrixTests.Token_env_and_percent_variables` | ✅ 1/1 |
| {folder} {path} {drive} {subdir} | `CaseMatrixTests.Token_path_tokens`<br>`TokenTests.Subdir_is_relative_to_root` | ✅ 4/4 · ✅ 1/1 |
| {desktop} {documents} {pictures} {music} {videos} {home} {downloads} | `CaseMatrixTests.Token_system_folders`<br>`CaseMatrixTests.Token_downloads_folder` | ✅ 6/6 · ✅ 1/1 |
| Formato data con "/" → sottocartelle | `TokenTests.Date_format_slash_creates_subfolders_in_paths` | ✅ 1/1 |
| Contatore per file e con zeri | `TokenTests.Counter_increments_per_file_and_pads` | ✅ 1/1 |
| Caratteri non validi sanificati, errori su token sconosciuti | `TokenTests.Invalid_characters_and_trailing_dots_are_sanitized`<br>`TokenTests.Errors_for_unknown_tokens_filters_and_unclosed` | ✅ 1/1 · ✅ 1/1 |

## Trigger

| Caso | Test | Esito |
|---|---|---|
| Watcher in tempo reale (file stabile) | `CaseMatrixTests.Trigger_realtime_watcher_raises_files_ready_after_file_is_stable` | ✅ 1/1 |
| Watcher: ignora download parziali e attende file bloccati | `CaseMatrixTests.Trigger_realtime_ignores_partial_downloads_and_waits_for_unlock` | ✅ 1/1 |
| Manuale (scansione) | `CaseMatrixTests.Trigger_manual_scan_lists_folder_files`<br>`EndToEndTests.Scan_from_command_line_organizes_monitored_folder` | ✅ 1/1 · ✅ 1/1 |
| Pianificato: a intervalli e giornaliero | `CaseMatrixTests.Trigger_scheduled_runs_due_folders`<br>`ServicesTests.Schedule_due_logic` | ✅ 2/2 · ✅ 1/1 |
| Monitoraggio in pausa | `CaseMatrixTests.Trigger_paused_monitor_does_nothing` | ✅ 1/1 |
| Drop target (icona flottante) e cartelle trascinate | `InProcessUiTests.Drop_on_floating_target_organizes_files_and_undo_restores_them`<br>`InProcessUiTests.Drop_of_a_folder_expands_to_its_files` | ✅ 1/1 · ✅ 1/1 |
| Menu contestuale di Explorer (registro + comando reale) | `InProcessUiTests.Context_menu_registers_three_verbs_and_unregisters`<br>`EndToEndTests.Explorer_context_menu_command_organizes_the_selected_file` | ✅ 1/1 · ✅ 1/1 |

## Preset, simulazione, conflitti, undo, log

| Caso | Test | Esito |
|---|---|---|
| Preset: esportazione/importazione JSON di ogni preset | `CaseMatrixTests.Preset_export_import_roundtrip_for_every_preset`<br>`ServicesTests.Profile_export_import_roundtrip_keeps_polymorphic_rules` | ✅ 1/1 · ✅ 1/1 |
| Preset: JSON non valido rifiutato | `CaseMatrixTests.Preset_import_rejects_invalid_json` | ✅ 1/1 |
| Preset pronti (Download, Foto, Documenti, Musica) | `ServicesTests.All_presets_build_and_match_expected_files`<br>`ServicesTests.Music_preset_uses_id3_with_defaults` | ✅ 1/1 · ✅ 1/1 |
| Simulazione: nessuna modifica, stesso esito del reale, conflitti previsti | `CaseMatrixTests.Simulation_reports_planned_operations_without_touching_files`<br>`EngineAndUndoTests.Simulation_changes_nothing_and_matches_real_run`<br>`EngineAndUndoTests.Simulation_detects_conflicts_between_files_of_the_same_run` | ✅ 1/1 · ✅ 1/1 · ✅ 1/1 |
| Conflitto: tieni entrambi (rinomina) | `CaseMatrixTests.Conflict_keep_both_renames_new`<br>`ConflictTests.Rename_new_finds_first_free_number` | ✅ 1/1 · ✅ 1/1 |
| Conflitto: sostituisci (vecchio nel Cestino; rifiutato senza Cestino) | `CaseMatrixTests.Conflict_overwrite_recycles_existing`<br>`ConflictTests.Engine_overwrite_is_refused_without_recycle_bin` | ✅ 1/1 · ✅ 1/1 |
| Conflitto: salta | `CaseMatrixTests.Conflict_skip_leaves_both_untouched`<br>`ConflictTests.Engine_skip_leaves_everything_untouched_and_stops_chain` | ✅ 1/1 · ✅ 1/1 |
| Undo (anche dopo riavvio, copie modificate preservate) | `CaseMatrixTests.Undo_restores_moved_and_recycled_files`<br>`EngineAndUndoTests.Undo_reverts_move_rename_and_removes_created_folders`<br>`EngineAndUndoTests.Undo_restores_overwritten_file_from_recycle_bin`<br>`EngineAndUndoTests.Undo_of_copy_recycles_the_copy_but_not_if_modified`<br>`EngineAndUndoTests.Journal_persists_and_caps_batches` | ✅ 1/1 · ✅ 1/1 · ✅ 1/1 · ✅ 1/1 · ✅ 1/1 |
| Log attività (memoria + file giornaliero, esportazione CSV) | `CaseMatrixTests.Activity_log_keeps_memory_and_daily_file`<br>`ServicesTests.Activity_log_exports_csv_with_escaping` | ✅ 1/1 · ✅ 1/1 |
| Cestino di Windows reale (round-trip, rete non supportata) | `WindowsRecycleBinTests.Recycle_and_restore_roundtrip`<br>`WindowsRecycleBinTests.Network_paths_are_not_supported` | ✅ 1/1 · ✅ 1/1 |
| Impostazioni corrotte → valori predefiniti | `ServicesTests.Corrupt_settings_fall_back_to_defaults_and_are_kept` | ✅ 1/1 |

## Robustezza

| Caso | Test | Esito |
|---|---|---|
| Percorsi lunghi (> 260 caratteri) | `CaseMatrixTests.Long_path_move_and_undo`<br>`EngineAndUndoTests.Long_paths_are_supported` | ✅ 1/1 · ✅ 1/1 |
| File in uso | `CaseMatrixTests.File_in_use_is_skipped_and_reported`<br>`EngineAndUndoTests.Locked_file_is_reported_and_other_files_continue` | ✅ 1/1 · ✅ 1/1 |
| Permessi negati (destinazione e file sorgente) | `CaseMatrixTests.Permission_denied_destination_is_reported_and_source_kept`<br>`CaseMatrixTests.Permission_denied_source_file_is_reported` | ✅ 1/1 · ✅ 1/1 |
| Unicode (accenti, greco, CJK, emoji) | `CaseMatrixTests.Unicode_paths_move_rename_and_undo`<br>`TokenTests.Unicode_names_are_preserved` | ✅ 1/1 · ✅ 1/1 |
| Eliminazione rifiutata dove non c'è Cestino | `EngineAndUndoTests.Delete_is_refused_where_recycle_bin_is_unavailable` | ✅ 1/1 |

## Tray, avvio con Windows, localizzazione

| Caso | Test | Esito |
|---|---|---|
| Menu dell'icona di notifica (apri, scansiona, pausa/riprendi, icona flottante, annulla, esci) | `InProcessUiTests.Tray_menu_offers_open_scan_pause_drop_undo_exit` | ✅ 1/1 |
| Avvio ridotto nel tray + istanza singola che inoltra gli argomenti | `EndToEndTests.Minimized_start_stays_in_tray_and_second_launch_shows_the_window` | ✅ 1/1 |
| Avvio con Windows (chiave Run, scrittura e rimozione) | `InProcessUiTests.Autostart_with_windows_writes_and_removes_run_entry` | ✅ 1/1 |
| Localizzazione IT/EN: tutte le stringhe usate esistono in entrambe le lingue | `InProcessUiTests.Every_string_key_used_by_the_ui_exists_in_both_languages`<br>`CaseMatrixTests.Localization_core_strings_exist_in_both_languages` | ✅ 1/1 · ✅ 1/1 |
| Cambio lingua a runtime / avvio in inglese | `InProcessUiTests.Language_switch_updates_ui_strings`<br>`EndToEndTests.English_ui_when_language_is_en` | ✅ 1/1 · ✅ 1/1 |

## Interfaccia (test automatici UI)

| Caso | Test | Esito |
|---|---|---|
| Avvio: finestra compatta 450×660, titlebar custom, apre su Home | `InProcessUiTests.Startup_main_window_is_compact_and_opens_on_home`<br>`EndToEndTests.Startup_shows_main_window_on_home` | ✅ 1/1 · ✅ 1/1 |
| Navigazione tra tutte le pagine (rail, UI Automation) | `InProcessUiTests.Navigation_reaches_every_page_and_keeps_one_selection`<br>`EndToEndTests.Navigation_through_the_rail_with_ui_automation` | ✅ 1/1 · ✅ 1/1 |
| Tastiera: Ctrl+1…8 | `InProcessUiTests.Keyboard_ctrl_digits_navigate` | ✅ 1/1 |
| Nomi accessibili e AutomationId del rail | `InProcessUiTests.Rail_items_have_accessible_names_and_ids` | ✅ 1/1 |
| Home: file in attesa, regole attive, banner | `InProcessUiTests.Home_shows_waiting_files_and_active_rules` | ✅ 1/1 |
| Tema chiaro/scuro: palette e rendering di ogni pagina | `InProcessUiTests.Theme_light_and_dark_switch_palette_and_render_every_page`<br>`EndToEndTests.Dark_theme_start_renders_dark_and_light_renders_light` | ✅ 1/1 · ✅ 1/1 |
| Contrasto WCAG AA (testo ≥ 4.5:1, focus ≥ 3:1) in chiaro e scuro | `InProcessUiTests.Palette_meets_WCAG_AA_contrast` | ✅ 2/2 |

## Verifiche di pacchetto (script, fuori da xUnit)

| Caso | Script | Esito |
|---|---|---|
| Installer: installazione/disinstallazione silenziosa pulita | `tests/verify-installer.ps1` | vedi riepilogo finale |
| Portable: avvio senza installazione, dati accanto all'exe | `tests/verify-portable.ps1` | vedi riepilogo finale |
