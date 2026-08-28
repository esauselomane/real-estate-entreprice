import React, { useState, useEffect } from 'react';
import { Sparkles, Printer, Image, CheckCircle2, User, Phone, Mail, Building2, MapPin, Layers, AlertCircle, Loader2, ArrowLeft } from 'lucide-react';
import { propertyService } from '../services/ propertyService.js';

export default function BrochureBuilderWorkspace({ dealData, onBack }) {
  // Master asset tracking state grids
  const [propertyDetails, setPropertyDetails] = useState(null);
  const [selectedImageFiles, setSelectedImageFiles] = useState([]);
  
  // UI orchestration feedback indicators
  const [isCompilingPdf, setIsCompilingPdf] = useState(false);
  const [isLoadingAsset, setIsLoadingAsset] = useState(true);
  const [feedbackMessage, setFeedbackMessage] = useState('');

  const activeProfile = JSON.parse(localStorage.getItem('user_profile_data') || '{}');
  const brokerProfile = {
    fullName: activeProfile.name || 'Unassigned broker',
    emailAddress: activeProfile.email || 'No email on file',
    phoneNumber: activeProfile.phone || 'No phone on file',
    companyReferenceNumber: `PROP-REF-${dealData?.dealId || '991'}`,
    companyName: activeProfile.tenantId || 'Agency workspace'
  };

  useEffect(() => {
    const fetchTargetAssetDetails = async () => {
      setIsLoadingAsset(true);
      try {
        if (!dealData?.linkedPropertyId) throw new Error('The deal has no linked property. Link a property before creating a brochure.');
        setPropertyDetails(await propertyService.getPropertyById(dealData.linkedPropertyId));
      } catch (err) {
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

  // --- 📨 DISPATCH ASYNC PAYLOAD TO REACTION KAFKA EVENT QUEUES ---
  const handleTriggerKafkaGeneration = async (e) => {
    e.preventDefault();
    setIsCompilingPdf(true);
    setFeedbackMessage('');

    try {
      const payloadEnvelope = {
        DealId: dealData?.dealId || 0,
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
        SubUnits: propertyDetails?.subUnits || []
      };

      await propertyService.dispatchBrochureGenerationTask(payloadEnvelope);
      setFeedbackMessage('Brochure generation request dispatched onto Kafka clusters! Worker processing has initiated.');
    } catch (err) {
      setFeedbackMessage(err || 'Failed to dispatch background event to event-driven processing pipelines.');
    } finally {
      setIsCompilingPdf(false);
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

  return (
    <div className="space-y-6 animate-fade-in pb-12">
      
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

        <button
          onClick={handleTriggerKafkaGeneration}
          disabled={isCompilingPdf}
          className="bg-blue-600 hover:bg-blue-500 disabled:bg-blue-800 text-white text-xs font-bold py-2.5 px-4 rounded-xl shadow-lg transition-colors cursor-pointer flex items-center gap-2"
        >
          {isCompilingPdf ? <Loader2 size={14} className="animate-spin" /> : <Printer size={14} />}
          {isCompilingPdf ? 'Queue Processing...' : 'Compile Brochure Task'}
        </button>
      </div>

      {feedbackMessage && (
        <div className="p-4 bg-blue-950/40 border border-blue-900 text-blue-400 rounded-2xl text-xs font-medium text-center animate-fade-in flex items-center justify-center gap-2">
          <Sparkles size={14} className="animate-pulse" /> {feedbackMessage}
        </div>
      )}

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
        </div>
        {/* RIGHT COLUMN PANEL: MASTER ASSET FINANCIAL MATRIX & DYNAMIC SCHEMAS */}
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
          {/* 📋 MULTI-TENANT GRANULAR SUB-UNIT INVENTORY BREAKDOWN GRID */}
          <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5 shadow-xl space-y-4">
            <h3 className="text-xs font-bold text-slate-400 uppercase tracking-wider flex items-center gap-1.5 border-b border-slate-800/60 pb-2">
              <Layers size={14} className="text-blue-400" /> Sub-Unit Space Layout Pages Breakdown
            </h3>
            
            <div className="space-y-3">
              {propertyDetails?.subUnits && propertyDetails.subUnits.length > 0 ? (
                propertyDetails.subUnits.map((unit) => (
                  <div key={unit.propertyUnitId} className="p-3 bg-slate-950/40 border border-slate-800/80 rounded-xl flex justify-between items-center text-xs">
                    <div>
                      <p className="font-bold text-white">{unit.unitNumber} — <span className="text-slate-400 font-medium">{unit.netRentableAreaSqM} m² NRA</span></p>
                      <p className="text-[11px] text-slate-500 mt-0.5 truncate max-w-lg">{unit.unitDescription}</p>
                    </div>
                    <span className="text-[10px] font-bold px-2 py-0.5 rounded border border-slate-800 bg-slate-900 text-slate-400 font-mono">
                      Page Index Attached
                    </span>
                  </div>
                ))
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
