import { useState, useEffect, useRef } from 'react';
import { Sparkles, Printer, Image, CheckCircle2, User, Phone, Mail, Building2, MapPin, Layers, Loader2, ArrowLeft, AlertTriangle } from 'lucide-react';
import { propertyService } from '../services/propertyService.js';
import { crmService } from '../services/crmService.js';
import { useAuth } from '../contexts/AuthContext.jsx';
import { isTrialUser, getTrialExpiry } from '../services/authService.js';

export default function BrochureBuilderWorkspace({ dealData, onBack }) {
  const { activeProfile } = useAuth();
  // Master asset tracking state grids
  const [propertyDetails, setPropertyDetails] = useState(null);
  const [selectedImageFiles, setSelectedImageFiles] = useState([]);

  // Broker-curated brochure card (cart) state
  const [selectedUnits, setSelectedUnits] = useState([]);
  const [clientInfo, setClientInfo] = useState({ name: '', email: '', phone: '' });
  const [cardId, setCardId] = useState(null);
  const [cardStatus, setCardStatus] = useState('idle'); // idle | generating | generated | failed

  // UI orchestration feedback indicators
  const [isCompilingPdf, setIsCompilingPdf] = useState(false);
  const [isLoadingAsset, setIsLoadingAsset] = useState(true);
  const [feedbackMessage, setFeedbackMessage] = useState('');

  const pollRef = useRef(null);

  const brokerProfile = {
    fullName: activeProfile.name || 'Unassigned broker',
    emailAddress: activeProfile.email || 'No email on file',
    phoneNumber: activeProfile.phone || 'No phone on file',
    companyReferenceNumber: `PROP-REF-${dealData?.dealId || '991'}`,
    companyName: activeProfile.tenantId || 'Agency workspace'
  };

  const isOnTrial = isTrialUser();
  const trialExpiresAt = getTrialExpiry();
  const isTrialExpired = isOnTrial && trialExpiresAt && new Date(trialExpiresAt) < new Date();

  const formatTrialExpiry = (expiry) => {
    if (!expiry) return '';
    try {
      return new Date(expiry).toLocaleDateString('en-ZA', { year: 'numeric', month: 'short', day: 'numeric' });
    } catch { return ''; }
  };

  // --- 📡 RESUME an existing brochure card when the builder is re-opened on a deal ---
  useEffect(() => {
    if (!dealData?.dealId) return;
    (async () => {
      try {
        const card = await crmService.getBrochureCardByDeal(dealData.dealId);
        if (card) {
          setCardId(card.cardId);
          setClientInfo({ name: card.clientName || '', email: card.clientEmail || '', phone: card.clientPhone || '' });
          setCardStatus((card.status || 'active').toLowerCase());
          if (Array.isArray(card.lines) && card.lines.length) {
            setSelectedUnits(card.lines.map(l => ({
              propertyUnitId: l.unitId,
              unitNumber: l.unitNumber,
              unitDescription: l.unitDescription,
              netRentableAreaSqM: l.netRentableAreaSqM
            })));
          }
        }
      } catch { /* no card yet — clean canvas */ }
    })();
  }, [dealData]);

  useEffect(() => {
    const fetchTargetAssetDetails = async () => {
      setIsLoadingAsset(true);
      try {
        if (!dealData?.linkedPropertyId) throw new Error('The deal has no linked property. Link a property before creating a brochure.');
        setPropertyDetails(await propertyService.getPropertyById(dealData.linkedPropertyId));
      } catch {
        setFeedbackMessage('Failed to pull asset metrics for proposal compiling.');
      } finally {
        setIsLoadingAsset(false);
      }
    };
    fetchTargetAssetDetails();
  }, [dealData]);

  // --- 🖼️ IMAGE COLLECTION CONSTRAINT CHECK ARRAY SELECTION HANDLER ---
  const handleToggleImageSelection = (imgUrl) => {
    if (selectedImageFiles.includes(imgUrl)) {
      setSelectedImageFiles(selectedImageFiles.filter(item => item !== imgUrl));
    } else {
      if (selectedImageFiles.length >= 5) {
        alert("Marketing Brochure design parameters limit layout sheets to 5 attached pictures maximum.");
        return;
      }
      setSelectedImageFiles([...selectedImageFiles, imgUrl]);
    }
  };

  // --- 📦 SUB-UNIT (PROPERTY UNIT) PRESENTATION SELECTION HANDLER (CAP TO 5) ---
  const handleToggleUnit = (unit) => {
    if (selectedUnits.some(u => u.propertyUnitId === unit.propertyUnitId)) {
      setSelectedUnits(selectedUnits.filter(u => u.propertyUnitId !== unit.propertyUnitId));
    } else {
      if (selectedUnits.length >= 5) {
        alert("Presentation constraints limit the brochure to 5 curated property units per client deck.");
        return;
      }
      setSelectedUnits([...selectedUnits, unit]);
    }
  };

  const handleRemoveUnit = async (unitId) => {
    setSelectedUnits(selectedUnits.filter(u => u.propertyUnitId !== unitId));
    // Persist the removal onto the live card immediately if one already exists.
    if (cardId) {
      try { await crmService.removeBrochureCardLine(cardId, unitId); }
      catch (err) { setFeedbackMessage(err || 'Could not remove unit from card.'); }
    }
  };

  // --- 📨 PERSIST CARD -> DISPATCH KAFKA -> POLL FOR WORKER COMPLETION ---
   const handleTriggerKafkaGeneration = async (e) => {
     e.preventDefault();

     if (isTrialExpired) {
       setFeedbackMessage('Your trial has expired. Upgrade to a paid plan to generate brochures.');
       return;
     }

     if (!selectedUnits.length) {
      setFeedbackMessage('Select at least one property unit to present to the client.');
      return;
    }
    if (!clientInfo.name.trim()) {
      setFeedbackMessage('Enter the client name before generating the brochure.');
      return;
    }
    setIsCompilingPdf(true);
    setFeedbackMessage('');
    setCardStatus('generating');

    try {
      // 1. Persist / upsert the broker's card (client details + curated unit line items).
      const savedCard = await crmService.saveBrochureCard({
        dealId: dealData.dealId,
        clientName: clientInfo.name,
        clientEmail: clientInfo.email,
        clientPhone: clientInfo.phone,
        units: selectedUnits.map(u => ({
          propertyId: propertyDetails?.propertyId || 0,
          unitId: u.propertyUnitId,
          unitNumber: u.unitNumber,
          unitDescription: u.unitDescription,
          netRentableAreaSqM: u.netRentableAreaSqM,
          imageRef: ''
        }))
      });
      const newCardId = savedCard?.cardId || cardId;
      setCardId(newCardId);

      // 2. Build the EXACT worker payload the BrochureGeneration.Worker consumes.
      //    SelectedImages is now a flat array (not a JSON string); SubUnits is the
      //    broker-curated <=5 set; BrochureCardId lets the worker callback to close the card.
      const payloadEnvelope = {
        DealId: dealData.dealId || 0,
        PropertyId: propertyDetails?.propertyId || 0,
        BrokerName: brokerProfile.fullName,
        BrokerEmail: brokerProfile.emailAddress,
        BrokerPhone: brokerProfile.phoneNumber,
        CompanyReferenceNumber: brokerProfile.companyReferenceNumber,
        MarketClass: propertyDetails?.marketClass || 'Rental',
        TenantInstallationAllowance: propertyDetails?.tenantInstallationAllowance || 0,
        CompleteAddress: propertyDetails?.completeAddress || 'Unknown Address Spec',
        Latitude: propertyDetails?.latitude || 0,
        Longitude: propertyDetails?.longitude || 0,
        SelectedImages: selectedImageFiles,
        SubUnits: selectedUnits.map(u => ({
          UnitNumber: u.unitNumber,
          UnitDescription: u.unitDescription,
          NetRentableAreaSqM: u.netRentableAreaSqM
        })),
        BrochureCardId: newCardId,
        ClientName: clientInfo.name,
        ClientEmail: clientInfo.email,
        ClientPhone: clientInfo.phone,
        BaseRentalRate: propertyDetails?.baseRentalRate || 0,
        RentEscalationPercentage: propertyDetails?.rentEscalationPercentage || 0,
        LeaseTermMonths: propertyDetails?.leaseTermMonths || 12,
        SalePrice: propertyDetails?.salePrice || 0
      };

      // 3. Dispatch onto the brochure-generation Kafka topic (Property -> Worker).
      await propertyService.dispatchBrochureGenerationTask(payloadEnvelope);
      setFeedbackMessage('Brochure request dispatched to Kafka. The worker is compiling the PDF...');

      // 4. Poll the CRM: when the worker callback fires it soft-deletes the card and
      //    advances the deal, which makes the card disappear (resume = null).
      pollRef.current = setInterval(async () => {
        try {
          const card = await crmService.getBrochureCardByDeal(dealData.dealId);
          if (!card) {
            clearInterval(pollRef.current);
            setCardStatus('generated');
            setIsCompilingPdf(false);
            setFeedbackMessage('✅ Brochure generated and saved to the server brochure folder. The card has been closed and the deal advanced to Proposal / Quote Sent.');
          } else if (card.status === 'Generated') {
            clearInterval(pollRef.current);
            setCardStatus('generated');
            setIsCompilingPdf(false);
            setFeedbackMessage('✅ Brochure generated. Card closed and deal advanced.');
          }
        } catch { /* transient — keep polling */ }
      }, 3000);

      // Safety timeout so the deck never polls forever.
      setTimeout(() => {
        if (isCompilingPdf) {
          clearInterval(pollRef.current);
          setIsCompilingPdf(false);
          setCardStatus('failed');
          setFeedbackMessage('Generation timed out — see the worker logs. The card remains active and can be regenerated.');
        }
      }, 60000);
    } catch (err) {
      clearInterval(pollRef.current);
      setIsCompilingPdf(false);
      setCardStatus('failed');
      setFeedbackMessage(err || 'Failed to dispatch the background generation event.');
    }
  };

  if (isLoadingAsset) {
    return (
      <div className="py-20 flex flex-col items-center justify-center text-slate-500 gap-3">
        <Loader2 className="w-8 h-8 text-blue-500 animate-spin" />
        <span className="text-xs font-medium tracking-wide">Extracting property profile data maps...</span>
      </div>
    );
  }

  const activeUnitsCount = selectedUnits.length;

  return (
    <div className="space-y-6 animate-fade-in pb-12">

      {isOnTrial && !isTrialExpired && (
        <div className="flex items-start gap-3 rounded-2xl bg-amber-950/50 border border-amber-900 p-4">
          <AlertTriangle size={20} className="text-amber-400 shrink-0 mt-0.5" />
          <div>
            <p className="text-xs font-bold text-amber-300 uppercase tracking-wider">Trial Period Active</p>
            <p className="text-xs text-amber-200 mt-0.5">
              Your 14-day free trial expires on {formatTrialExpiry(trialExpiresAt)}.
              Upgrade to a paid plan to continue generating brochures after expiry.
            </p>
          </div>
        </div>
      )}
      {isTrialExpired && (
        <div className="flex items-start gap-3 rounded-2xl bg-red-950/50 border border-red-900 p-4">
          <AlertTriangle size={20} className="text-red-400 shrink-0 mt-0.5" />
          <div>
            <p className="text-xs font-bold text-red-300 uppercase tracking-wider">Trial Expired</p>
            <p className="text-xs text-red-200 mt-0.5">
              Your trial period has ended. Please upgrade to a paid plan to continue using this feature.
            </p>
          </div>
        </div>
      )}

      {/* 🧭 CONTROL NAVIGATION ACTION PANEL BAR ROW */}
      <div className="flex items-center justify-between border-b border-slate-800 pb-4">
        <div className="flex items-center gap-3">
          <button
            onClick={onBack}
            className="p-2 bg-slate-950 hover:bg-slate-800 border border-slate-800 rounded-xl text-slate-400 hover:text-white transition-colors cursor-pointer"
          >
            <ArrowLeft size={16} />
          </button>
          <div>
            <h2 className="text-xl font-black text-white tracking-tight">Brochure Generator Deck</h2>
            <p className="text-xs text-slate-400 font-medium mt-0.5">Asynchronous Event-Driven Proposal Platform Compiler</p>
          </div>
        </div>

        {cardStatus === 'generated' && (
          <span className="text-[10px] font-bold font-mono text-emerald-400 bg-emerald-950/40 px-2.5 py-1 rounded-lg border border-emerald-900">
            ✅ BROCHURE GENERATED
          </span>
        )}
        {cardStatus === 'generating' && (
          <span className="text-[10px] font-bold font-mono text-amber-400 bg-amber-950/40 px-2.5 py-1 rounded-lg border border-amber-900">
            ⏳ COMPILING VIA WORKER
          </span>
        )}

        <button
          onClick={handleTriggerKafkaGeneration}
          disabled={isCompilingPdf || cardStatus === 'generated' || isTrialExpired}
          className="bg-blue-600 hover:bg-blue-500 disabled:bg-blue-800 text-white text-xs font-bold py-2.5 px-4 rounded-xl shadow-lg transition-colors cursor-pointer flex items-center gap-2"
        >
          {isCompilingPdf ? <Loader2 size={14} className="animate-spin" /> : <Printer size={14} />}
          {isCompilingPdf ? 'Queue Processing...' : cardStatus === 'generated' ? 'Regenerate Brochure' : 'Compile Brochure Task'}
        </button>
      </div>

      {feedbackMessage && (
        <div className="p-4 bg-blue-950/40 border border-blue-900 text-blue-400 rounded-2xl text-xs font-medium text-center animate-fade-in flex items-center justify-center gap-2">
          <Sparkles size={14} className="animate-pulse" /> {feedbackMessage}
        </div>
      )}

      {/* 📋 CLIENT PRESENTATION DETAILS (broker-curated) */}
      <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5 shadow-xl space-y-4">
        <h3 className="text-xs font-bold text-slate-400 uppercase tracking-wider flex items-center gap-1.5 border-b border-slate-800/60 pb-2">
          <User size={14} className="text-blue-400" /> Client Presentation Recipients
        </h3>
        <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
          <div>
            <label className="block text-[10px] text-slate-500 font-bold uppercase tracking-wider mb-1.5">Recipient Name</label>
            <input type="text" value={clientInfo.name}
              onChange={(e) => setClientInfo({ ...clientInfo, name: e.target.value })}
              className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500"
              placeholder="Attn: Client decision maker" />
          </div>
          <div>
            <label className="block text-[10px] text-slate-500 font-bold uppercase tracking-wider mb-1.5">Email Address</label>
            <input type="email" value={clientInfo.email}
              onChange={(e) => setClientInfo({ ...clientInfo, email: e.target.value })}
              className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500"
              placeholder="client@company.com" />
          </div>
          <div>
            <label className="block text-[10px] text-slate-500 font-bold uppercase tracking-wider mb-1.5">Contact Number</label>
            <input type="tel" value={clientInfo.phone}
              onChange={(e) => setClientInfo({ ...clientInfo, phone: e.target.value })}
              className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500"
              placeholder="+27 82 000 0000" />
          </div>
        </div>
      </div>

      {/* 📊 GRID LAYOUT SPLIT PANELS */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">

        {/* LEFT COLUMN PANEL: COVER SHEET PREVIEW BROKER META */}
        <div className="lg:col-span-1 space-y-4">
          <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5 shadow-xl space-y-4">
            <h3 className="text-xs font-bold text-slate-400 uppercase tracking-wider flex items-center gap-1.5 border-b border-slate-800/60 pb-2">
              <User size={14} className="text-blue-400" /> Page 1: Broker Cover Summary
            </h3>

            <div className="bg-slate-950/80 border border-slate-800 rounded-xl p-4 space-y-3">
              <div className="flex items-center gap-3">
                <div className="w-10 h-10 rounded-xl bg-blue-600 font-black text-white flex items-center justify-center text-sm shadow-md">
                  {brokerProfile.companyName.split(/\s+/).map((part) => part[0]).join('').slice(0, 3).toUpperCase()}
                </div>
                <div>
                  <h4 className="text-white font-bold text-sm tracking-tight">{brokerProfile.companyName}</h4>
                  <span className="text-[10px] font-mono text-slate-500">Ref: {brokerProfile.companyReferenceNumber}</span>
                </div>
              </div>
              <div className="pt-2 border-t border-slate-800/60 space-y-2 text-xs text-slate-400 font-medium">
                <p className="flex items-center gap-2"><User size={12} className="text-slate-500" /> {brokerProfile.fullName}</p>
                <p className="flex items-center gap-2"><Mail size={12} className="text-slate-500" /> {brokerProfile.emailAddress}</p>
                <p className="flex items-center gap-2"><Phone size={12} className="text-slate-500" /> {brokerProfile.phoneNumber}</p>
              </div>
            </div>
          </div>

          {/* 🧾 SELECTED UNIT LINE-ITEMS SUMMARY (cart) */}
          <div className="bg-slate-900 border border-slate-800 rounded-2xl p-4 shadow-xl space-y-3">
            <h3 className="text-[10px] font-bold text-slate-400 uppercase tracking-wider flex items-center gap-1.5">
              <Layers size={14} className="text-blue-400" /> Curated Unit Selection ({activeUnitsCount}/5)
            </h3>
            {selectedUnits.length ? (
              <div className="space-y-2">
                {selectedUnits.map((u) => (
                  <div key={u.propertyUnitId} className="flex items-center justify-between p-2 bg-slate-950/40 border border-slate-800/80 rounded-lg text-xs">
                    <span className="text-blue-300 font-mono">{u.unitNumber} · {u.netRentableAreaSqM} m²</span>
                    <button
                      onClick={() => handleRemoveUnit(u.propertyUnitId)}
                      className="text-[10px] text-red-400 hover:text-red-300 cursor-pointer"
                      title="Remove this unit from the brochure deck"
                    >✕</button>
                  </div>
                ))}
              </div>
            ) : (
              <p className="text-[10px] text-slate-500 italic">No units selected yet.</p>
            )}
          </div>
        </div>

        {/* RIGHT COLUMN PANEL: MASTER ASSET + UNIT SELECTION + IMAGES */}
        <div className="lg:col-span-2 space-y-4">

          {/* MASTER ASSET DETAILS & LOCATION PAIRS */}
          <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5 shadow-xl space-y-4">
            <h3 className="text-xs font-bold text-slate-400 uppercase tracking-wider flex items-center gap-1.5 border-b border-slate-800/60 pb-2">
              <Building2 size={14} className="text-blue-400" /> Page 2 & 3: Asset Specifications
            </h3>

            <div className="grid grid-cols-1 md:grid-cols-2 gap-4 text-xs text-slate-300">
              <div className="space-y-2 bg-slate-950/40 p-4 rounded-xl border border-slate-800/50">
                <span className="block text-[10px] text-slate-500 font-bold uppercase tracking-wider">Geographic Address</span>
                <p className="font-bold text-white flex items-start gap-1.5"><MapPin size={14} className="text-red-400 shrink-0 mt-0.5" /> {propertyDetails?.completeAddress}</p>
                <div className="pt-2 text-[10px] font-mono text-slate-500 flex gap-4">
                  <span>Lat: {propertyDetails?.latitude}</span>
                  <span>Lng: {propertyDetails?.longitude}</span>
                </div>
              </div>

              {/* Dynamic Incentives Matrix Box */}
              <div className="space-y-2 bg-blue-950/20 p-4 rounded-xl border border-blue-900/40">
                <span className="block text-[10px] text-blue-400 font-bold uppercase tracking-wider">Tenant Incentive Allocation</span>
                <p className="text-lg font-black text-white">R {propertyDetails?.tenantInstallationAllowance?.toLocaleString() || '0.00'}</p>
                <span className="block text-[10px] text-slate-400 font-medium">Tenant Installation (TI) Allowance Grade</span>
              </div>
            </div>

            {/* 🔀 CONDITIONAL FORKED SALES VS LEASE RENTAL INTERFACE LAYOUT ROW */}
            <div className="p-4 bg-slate-950/60 border border-slate-800 rounded-xl space-y-2 text-xs">
              <span className="block text-[10px] text-slate-500 font-bold uppercase tracking-wider mb-2">Forked Financial Operational Agreement Matrix</span>
              {propertyDetails?.marketClass === 'Rental' ? (
                <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
                  <div>
                    <span className="block text-[10px] text-slate-500">Base Monthly Rent</span>
                    <p className="font-bold text-amber-400 text-sm mt-0.5">R {propertyDetails?.baseRentalRate?.toLocaleString()} /mo</p>
                  </div>
                  <div>
                    <span className="block text-[10px] text-slate-500">Annual Escalation</span>
                    <p className="font-bold text-white text-sm mt-0.5">{propertyDetails?.rentEscalationPercentage}% Compound</p>
                  </div>
                  <div>
                    <span className="block text-[10px] text-slate-500">Lease Commitment</span>
                    <p className="font-bold text-white text-sm mt-0.5">{propertyDetails?.leaseTermMonths} Months Minimum</p>
                  </div>
                </div>
              ) : (
                <div>
                  <span className="block text-[10px] text-slate-500">Corporate Target Acquisition Sale Price</span>
                  <p className="font-black text-emerald-400 text-lg mt-0.5">R {propertyDetails?.salePrice?.toLocaleString()} (Excl VAT)</p>
                </div>
              )}
            </div>
          </div>

          {/* 📋 MULTI-TENANT GRANULAR SUB-UNIT INVENTORY WITH <=5 SELECTION */}
          <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5 shadow-xl space-y-4">
            <div className="flex justify-between items-center border-b border-slate-800/60 pb-2">
              <h3 className="text-xs font-bold text-slate-400 uppercase tracking-wider flex items-center gap-1.5">
                <Layers size={14} className="text-blue-400" /> Select Up To 5 Sub-Units To Present
              </h3>
              <span className="text-[10px] font-bold font-mono text-slate-400 bg-slate-950 px-2 py-1 border border-slate-800 rounded-lg">
                Selected: {activeUnitsCount} / 5
              </span>
            </div>

            <div className="space-y-3">
              {propertyDetails?.subUnits && propertyDetails.subUnits.length > 0 ? (
                propertyDetails.subUnits.map((unit) => {
                  const isChecked = selectedUnits.some(u => u.propertyUnitId === unit.propertyUnitId);
                  return (
                    <div key={unit.propertyUnitId} className={`p-3 bg-slate-950/40 border rounded-xl flex justify-between items-center text-xs transition-colors ${
                      isChecked ? 'border-blue-700 ring-1 ring-blue-700/30' : 'border-slate-800/80'
                    }`}>
                      <label className="flex items-center gap-3 cursor-pointer">
                        <input
                          type="checkbox"
                          checked={isChecked}
                          onChange={() => handleToggleUnit(unit)}
                          disabled={cardStatus === 'generated'}
                          className="w-4 h-4 rounded accent-blue-600 cursor-pointer"
                        />
                        <div>
                          <p className="font-bold text-white">{unit.unitNumber} — <span className="text-slate-400 font-medium">{unit.netRentableAreaSqM} m² NRA</span></p>
                          <p className="text-[11px] text-slate-500 mt-0.5 truncate max-w-lg">{unit.unitDescription}</p>
                        </div>
                      </label>
                      <span className="text-[10px] font-bold px-2 py-0.5 rounded border border-slate-800 bg-slate-900 text-slate-400 font-mono">
                        {isChecked ? 'Curated' : 'Available'}
                      </span>
                    </div>
                  );
                })
              ) : (
                <p className="text-xs text-slate-500 italic py-2">Consolidated residential single-asset structure profile. No isolated sub-unit sub-pages mapped.</p>
              )}
            </div>
          </div>

          {/* 🖼️ INTERACTIVE PHOTO ATTACHMENT SELECTOR DESK (CAP UP TO 5 PHOTOS) */}
          <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5 shadow-xl space-y-4">
            <div className="flex justify-between items-center border-b border-slate-800/60 pb-2">
              <h3 className="text-xs font-bold text-slate-400 uppercase tracking-wider flex items-center gap-1.5">
                <Image size={14} className="text-blue-400" /> Gallery Attachment Asset Selection
              </h3>
              <span className="text-[10px] font-bold font-mono text-slate-400 bg-slate-950 px-2 py-1 border border-slate-800 rounded-lg">
                Selected: {selectedImageFiles.length} / 5 Max
              </span>
            </div>

            <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 gap-3">
              {JSON.parse(propertyDetails?.verificationDocumentsJson || '[]').map((imgName, index) => {
                const isSelected = selectedImageFiles.includes(imgName);
                return (
                  <div
                    key={index}
                    onClick={() => handleToggleImageSelection(imgName)}
                    className={`relative aspect-video rounded-xl border-2 overflow-hidden bg-slate-950 flex flex-col justify-between p-2.5 cursor-pointer select-none transition-all group ${
                      isSelected
                        ? 'border-blue-500 ring-2 ring-blue-500/20 shadow-lg'
                        : 'border-slate-800 hover:border-slate-700'
                    }`}
                  >
                    <span className="text-[10px] font-mono text-slate-500 group-hover:text-slate-400 transition-colors truncate">
                      {imgName}
                    </span>

                    <div className="flex justify-end pt-4">
                      <div className={`w-4 h-4 rounded-md border flex items-center justify-center transition-colors ${
                        isSelected ? 'bg-blue-600 border-blue-500 text-white' : 'border-slate-700 bg-slate-900'
                      }`}>
                        {isSelected && <CheckCircle2 size={10} strokeWidth={3} />}
                      </div>
                    </div>
                  </div>
                );
              })}
              {!JSON.parse(propertyDetails?.verificationDocumentsJson || '[]').length && <p className="col-span-full text-xs text-slate-500">No saved property media is available for this brochure.</p>}
            </div>
          </div>

        </div>
      </div>

    </div>
  );
}
